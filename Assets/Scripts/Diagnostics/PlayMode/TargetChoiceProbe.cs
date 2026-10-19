#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // Development comparison only. Creates private runs; never reads or writes the live run or PlayerPrefs.
    public sealed class TargetChoiceProbe : MonoBehaviour
    {
        const string Folder="Builds/Verification/CreativeBeats";
        const int Seeds=80,Residents=9,Stages=12;
        const float Duration=90,ActionTime=8;
        static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        enum Policy { EveryoneTarget, Adaptive, AdaptiveThree, ObserveOnly }
        static readonly string[] PolicyNames={"全员持续单推","自适应选择","每阶段最多三人参考","教学后停推"};

        [Serializable] public sealed class Trial
        {
            public int seed,target,policy;
            public bool targetWon;
            public int targetWilling,targetActivities,targetLegacyOnly;
            public int direct,shares,targetShares,passed,overloaded,rejected;
            public int switchedTopicDirect,changedActivityByShare;
            public int availableDirectOpportunities,maxDirectInStage;
            public string openingFingerprint,resultFingerprint;
        }
        [Serializable] public sealed class Summary
        {
            public int policy,target=-1,trials;
            public string label;
            public float winRate,targetWilling,targetActivities,targetLegacyOnly,direct,shares,targetShares,rejected,passed,overloaded;
            public float targetWillingPerDirect;
        }
        [Serializable] public sealed class PairedComparison
        {
            public int baseline,alternative,pairs,seedBlocks,targetCountBetter,targetCountWorse,targetCountSame,winsBetter,winsWorse;
            public string label;
            public float targetCountDifference,targetCount95Lower,targetCount95Upper;
            public float winRateDifference,winRate95Lower,winRate95Upper,directDifference,rejectedDifference;
        }
        [Serializable] sealed class Report
        {
            public string completedUtc,method,limitations,interpretation;
            public int seeds=Seeds,targets=5,policies=4,stages=Stages,residents=Residents;
            public float stageDuration=Duration,actionAtSeconds=ActionTime,elapsedRealSeconds;
            public int completedTrials,reproducedTrials;
            public bool reproducible,identicalOpeningAndTeaching,equalOfficialOpportunities,experimentalThreeOnly;
            public SimulationParameters parameters;
            public Trial[] trials;
            public Summary[] summaries;
            public PairedComparison[] pairedComparisons;
        }
        struct Choice { public int resident,topic;public float gain; }

        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            var watch=System.Diagnostics.Stopwatch.StartNew();var trials=new List<Trial>();
            var checks=new List<string>();bool complete=false;
            try
            {
                for(int seed=1;seed<=Seeds;seed++)for(int target=0;target<5;target++)
                {
                    string opening=null;
                    for(int policy=0;policy<4;policy++)
                    {
                        var first=Simulate(seed,target,(Policy)policy);var repeated=Simulate(seed,target,(Policy)policy);
                        Require(first.resultFingerprint==repeated.resultFingerprint,"Same seed, policy and virtual timestamps reproduce exactly");
                        Require(first.direct==repeated.direct&&first.shares==repeated.shares&&first.targetWilling==repeated.targetWilling&&first.targetWon==repeated.targetWon,"Repeated metrics reproduce exactly");
                        if(opening==null)opening=first.openingFingerprint;
                        Require(first.openingFingerprint==opening,"All four strategies receive the same opening and tutorial result");
                        Require(first.availableDirectOpportunities==108&&first.maxDirectInStage<=9,"Official opportunity limits are identical");
                        Require(policy!=2||first.maxDirectInStage<=3,"The three-person cap exists only inside this experimental policy");
                        Require(policy!=0||first.direct==108,"Full-push uses all 108 official opportunities, including teaching");
                        Require(policy!=3||first.direct==1,"Observe-only submits just the common teaching recommendation");
                        trials.Add(first);
                        File.WriteAllText(Folder+"/TargetChoiceProgress.txt","RUNNING "+trials.Count+" / 1600 trials; every trial also rerun\nseed="+seed+" target="+target+"\nseconds="+watch.Elapsed.TotalSeconds.ToString("F1",Invariant));
                        yield return null;
                    }
                }
                var summaries=new List<Summary>();
                for(int policy=0;policy<4;policy++)
                {
                    summaries.Add(Aggregate(trials.Where(r=>r.policy==policy),policy,-1));
                    for(int target=0;target<5;target++)summaries.Add(Aggregate(trials.Where(r=>r.policy==policy&&r.target==target),policy,target));
                }
                var comparisons=new[]{Compare(trials,0,1),Compare(trials,0,2),Compare(trials,0,3),Compare(trials,1,2)};
                string interpretation=Interpret(comparisons);
                var report=new Report {
                    completedUtc=DateTime.UtcNow.ToString("O",Invariant),elapsedRealSeconds=(float)watch.Elapsed.TotalSeconds,
                    completedTrials=trials.Count,reproducedTrials=trials.Count,reproducible=true,
                    identicalOpeningAndTeaching=true,equalOfficialOpportunities=true,experimentalThreeOnly=true,
                    parameters=new SimulationParameters(),trials=trials.ToArray(),summaries=summaries.ToArray(),pairedComparisons=comparisons,
                    method="80 seeds x 5 targets x 4 policies, all rerun. Each private run uses the real ResidentRun, 9 residents, 12 x 90 seconds. Resident 301 receives the target at tutorial tick 0. All later player actions occur at tick 80 in each stage. Adaptive policies evaluate expected willingness margin from hidden interest/pressure/thresholds, including no action or another topic. The three-person experiment counts teaching inside stage 1's three actions. All policies retain 108 official available opportunities.",
                    limitations="The adaptive policy is a developer reference that sees hidden data, not evidence that blind players understand or can execute it. Receive uses one shared random stream: changing the number/order of contacts changes subsequent reaction-roll assignment, although openings and independent share schedules match. Paired 95% ranges use the normal approximation over 80 opening-seed blocks, each averaging all five targets; they are descriptive, not an independent human playtest. One fixed early action time does not cover late-stage timing strategies. No official rule, scene, live run or save is modified.",
                    interpretation=interpretation
                };
                WriteCsv(report);
                File.WriteAllText(Folder+"/TargetChoiceReport.json",JsonUtility.ToJson(report,true),new UTF8Encoding(false));
                File.WriteAllText(Folder+"/TargetChoiceConclusion.md",Conclusion(report),new UTF8Encoding(false));
                checks.Add("PASS 1600 genuine ResidentRun trials plus 1600 exact reruns; 80 common seeds and every target");
                checks.Add("PASS Same random opening, target, resident-301 teaching at tick 0 and stage action timestamps for every policy");
                checks.Add("PASS Same official 108 opportunities; no live rule changes; experimental three-person limit includes teaching");
                checks.Add("PASS Events and every stage's resident state reproduce exactly; stage 12 ends without stage 13");
                checks.Add("PASS No FrontendServices, PlayerPrefs or user-save access; all worlds are isolated diagnostic instances");
                checks.Add("COMPLETE "+report.completedUtc);
                File.WriteAllLines(Folder+"/TargetChoiceChecks.txt",checks,new UTF8Encoding(false));
                File.WriteAllText(Folder+"/TargetChoiceProgress.txt","COMPLETE "+report.completedUtc+"\n"+interpretation+"\nseconds="+watch.Elapsed.TotalSeconds.ToString("F1",Invariant));
                complete=true;Debug.Log("[TargetChoiceProbe] 1600 trials and reruns complete. "+interpretation);
            }
            finally
            {
                if(!complete)File.WriteAllText(Folder+"/TargetChoiceChecks.txt","FAILED OR INTERRUPTED: comparison did not finish; partial progress is not a result.\ncompleted="+trials.Count);
            }
            Destroy(this);
        }

        static Trial Simulate(int seed,int target,Policy policy)
        {
            var run=new ResidentRun(Residents,Duration,Stages,seed);var result=new Trial { seed=seed,target=target,policy=(int)policy };
            Require(run.SetTarget(target)&&run.Recommend(0,target)&&run.StartStage(),"The common teaching/setup must be accepted");
            result.availableDirectOpportunities=run.Available+1;
            var opening=new Fingerprint();opening.Add(seed);opening.Add(target);
            foreach(var resident in run.opening)opening.Add(resident);
            foreach(var resident in run.residents)opening.Add(resident);
            result.openingFingerprint=opening.Value;
            var digest=new Fingerprint();digest.Add(result.openingFingerprint);
            while(true)
            {
                run.Tick(ActionTime);Require(Math.Abs(run.Elapsed-ActionTime)<.001f,"Actions occur at the same virtual second in each stage");
                if(policy==Policy.EveryoneTarget)
                {
                    for(int i=0;i<Residents;i++)if(!run.residents[i].recommended)Require(run.Recommend(i,target),"Every eligible full-push submission must be accepted exactly once");
                }
                else if(policy==Policy.Adaptive||policy==Policy.AdaptiveThree)
                {
                    var choices=new List<Choice>();
                    for(int i=0;i<Residents;i++)
                    {
                        if(run.residents[i].recommended)continue;
                        float before=Utility(run.residents[i],target,-1,0,run.rules);var best=new Choice { resident=i,topic=-1,gain=.005f };
                        for(int topic=0;topic<5;topic++)
                        {
                            float gain=ExpectedUtility(run.residents[i],target,topic,run.rules)-before;
                            if(gain>best.gain+.000001f)best=new Choice { resident=i,topic=topic,gain=gain };
                        }
                        if(best.topic>=0)choices.Add(best);
                    }
                    choices.Sort((a,b)=>{int comparison=b.gain.CompareTo(a.gain);return comparison==0?a.resident.CompareTo(b.resident):comparison;});
                    int budget=(policy==Policy.AdaptiveThree?3:Residents)-run.residents.Count(r=>r.recommended);
                    foreach(var choice in choices.Take(Math.Max(0,budget)))Require(run.Recommend(choice.resident,choice.topic),"Each chosen resident receives one accepted submission");
                }
                run.FinishEarly();Require(run.StageEnded&&Math.Abs(run.Remaining)<.001f,"All real remaining time and share attempts settle");
                result.maxDirectInStage=Math.Max(result.maxDirectInStage,run.residents.Count(r=>r.recommended));
                foreach(var e in run.events)
                {
                    digest.Add(e.stage);digest.Add(e.tick);digest.Add(e.resident);digest.Add(e.topic);digest.Add(e.source);digest.Add(e.kind);digest.Add(e.reaction.GetHashCode());digest.Add(e.previousTopic);digest.Add(e.currentTopic);
                    if(e.resident>=0&&e.after!=null)digest.Add(e.after[e.resident]);
                    bool direct=e.kind=="玩家推荐",share=e.kind=="邻居分享";
                    if(!direct&&!share)continue;
                    if(direct) { result.direct++;if(e.topic!=target)result.switchedTopicDirect++; }
                    if(share) { result.shares++;if(e.topic==target)result.targetShares++;if(e.previousTopic!=e.currentTopic)result.changedActivityByShare++; }
                    if(e.reaction==ResidentReaction.Passed)result.passed++;
                    if(e.reaction==ResidentReaction.Overloaded)result.overloaded++;
                }
                foreach(var resident in run.residents)digest.Add(resident);
                // The diagnostic has already counted/digested these immutable records. Clearing only
                // this private history bounds memory; neither simulation eligibility nor RNG reads it.
                run.events.Clear();
                if(run.Finished)break;
                Require(run.NextStage(),"Each completed nonfinal stage must advance once");
                result.availableDirectOpportunities+=run.Available;
            }
            result.targetWon=run.TargetWon();result.targetWilling=run.Scores()[target];
            result.targetActivities=run.residents.Count(r=>r.preferredTopic==target);
            result.targetLegacyOnly=run.residents.Count(r=>r.propsUntil[target]>=run.Stage&&r.WillingTopic()!=target);
            result.rejected=result.passed+result.overloaded;
            digest.Add(result.targetWon?1:0);foreach(int score in run.Scores())digest.Add(score);
            result.resultFingerprint=digest.Value;
            Require(run.Stage==12&&run.Finished&&!run.NextStage(),"Final standings come from actual willingness, with no stage 13");
            return result;
        }

        static float ExpectedUtility(ResidentState resident,int target,int delivered,SimulationParameters rules)
        {
            float interest=resident.interest[delivered];
            float chance=Clamp(.2f+interest*.67f-resident.PressureRatio(delivered)*.38f,.05f,.88f);
            if(Clamp(resident.saturation[delivered]+rules.pressureGain)/Math.Max(.1f,resident.aversionThresholds[delivered])>=1)
                return Utility(resident,target,delivered,-.015f,rules);
            float curiousChance=Math.Min(.22f,1-chance),passedChance=1-chance-curiousChance;
            return chance*Utility(resident,target,delivered,rules.interestGain,rules)
                +curiousChance*Utility(resident,target,delivered,.065f,rules)
                +passedChance*Utility(resident,target,delivered,0,rules);
        }
        static float Utility(ResidentState resident,int target,int delivered,float interestDelta,SimulationParameters rules)
        {
            float targetScore=0,competitor=0;
            for(int topic=0;topic<5;topic++)
            {
                float interest=resident.interest[topic],pressure=resident.saturation[topic];
                if(delivered>=0)
                {
                    pressure=Clamp(pressure+(topic==delivered?rules.pressureGain:-rules.otherTopicRelief));
                    if(topic==delivered)interest=Clamp(interest+interestDelta);
                }
                float score=interest*(1-.78f*Math.Min(1,pressure/Math.Max(.1f,resident.aversionThresholds[topic])));
                if(topic==target)targetScore=score;else competitor=Math.Max(competitor,score);
            }
            // A smooth margin favours converting undecided residents and resting secure supporters.
            // It is a deterministic heuristic, not knowledge of the coming random receive roll.
            return Clamp(.5f+(targetScore-competitor)/.24f);
        }
        static Summary Aggregate(IEnumerable<Trial> source,int policy,int target)
        {
            var rows=source.ToArray();return new Summary {
                policy=policy,target=target,label=PolicyNames[policy],trials=rows.Length,
                winRate=(float)rows.Average(r=>r.targetWon?1d:0d),targetWilling=(float)rows.Average(r=>r.targetWilling),
                targetActivities=(float)rows.Average(r=>r.targetActivities),targetLegacyOnly=(float)rows.Average(r=>r.targetLegacyOnly),
                direct=(float)rows.Average(r=>r.direct),shares=(float)rows.Average(r=>r.shares),targetShares=(float)rows.Average(r=>r.targetShares),
                rejected=(float)rows.Average(r=>r.rejected),passed=(float)rows.Average(r=>r.passed),overloaded=(float)rows.Average(r=>r.overloaded),
                targetWillingPerDirect=(float)rows.Sum(r=>r.targetWilling)/Math.Max(1,rows.Sum(r=>r.direct))
            };
        }
        static PairedComparison Compare(List<Trial> trials,int baseline,int alternative)
        {
            var before=trials.Where(r=>r.policy==baseline).OrderBy(r=>r.seed).ThenBy(r=>r.target).ToArray();
            var after=trials.Where(r=>r.policy==alternative).OrderBy(r=>r.seed).ThenBy(r=>r.target).ToArray();
            var count=new double[before.Length];var wins=new double[before.Length];
            for(int i=0;i<before.Length;i++) { Require(before[i].seed==after[i].seed&&before[i].target==after[i].target,"Paired rows match exactly");count[i]=after[i].targetWilling-before[i].targetWilling;wins[i]=(after[i].targetWon?1:0)-(before[i].targetWon?1:0); }
            var countBySeed=before.Select((r,i)=>new { r.seed,count=count[i],win=wins[i] }).GroupBy(r=>r.seed).Select(g=>new { count=g.Average(r=>r.count),win=g.Average(r=>r.win) }).ToArray();
            var result=new PairedComparison { baseline=baseline,alternative=alternative,pairs=before.Length,seedBlocks=countBySeed.Length,label=PolicyNames[alternative]+" 相对 "+PolicyNames[baseline],
                targetCountBetter=count.Count(x=>x>0),targetCountWorse=count.Count(x=>x<0),targetCountSame=count.Count(x=>x==0),winsBetter=wins.Count(x=>x>0),winsWorse=wins.Count(x=>x<0),
                targetCountDifference=(float)count.Average(),winRateDifference=(float)wins.Average(),directDifference=(float)after.Average(r=>r.direct)-(float)before.Average(r=>r.direct),rejectedDifference=(float)after.Average(r=>r.rejected)-(float)before.Average(r=>r.rejected) };
            float countRadius=Radius(countBySeed.Select(r=>r.count).ToArray()),winRadius=Radius(countBySeed.Select(r=>r.win).ToArray());
            result.targetCount95Lower=result.targetCountDifference-countRadius;result.targetCount95Upper=result.targetCountDifference+countRadius;
            result.winRate95Lower=result.winRateDifference-winRadius;result.winRate95Upper=result.winRateDifference+winRadius;return result;
        }
        static float Radius(double[] values)
        {
            double mean=values.Average(),sum=values.Sum(x=>(x-mean)*(x-mean));
            return values.Length<2?0:(float)(1.96*Math.Sqrt(sum/(values.Length-1)/values.Length));
        }
        static string Interpret(PairedComparison[] comparisons)
        {
            var adaptive=comparisons[0];
            string measured=adaptive.targetCount95Lower>0||adaptive.winRate95Lower>0
                ?"这批原模拟样本支持选择对象、停推或换话题具有可测收益；不需要先增加正式限额。"
                :adaptive.targetCountDifference>0||adaptive.winRateDifference>0
                    ?"选择性参考出现更好的平均结果，但差值区间尚跨过零；本批样本不足以证明稳定优势。"
                    :"本批样本没有证明此选择性参考比全推更好，需要调整参考策略或进一步检查机制；不能据此直接加入限额。";
            return measured+" 三次版本只是实验对照。开发参考能读取隐藏状态，是否能形成玩家取舍仍待相同开局盲测。";
        }
        static string Conclusion(Report report)
        {
            var text=new StringBuilder("# 选择对象：原模拟策略比较\n\n");
            text.Append(report.interpretation).Append("\n\n完成时间 UTC：").Append(report.completedUtc).Append("；1600 个完整对局，每个另跑一次，共 3200 次；实际运行 ").Append(F(report.elapsedRealSeconds)).Append(" 秒。\n\n");
            text.Append("四种策略共享 80 个开局、五种自选目标、301 的同一教学推荐、每阶段第 8 秒行动及完整 90 秒结算。正式机会始终是每人一次，全楼 108 次；三人版只在测试策略里限制每阶段三次，教学包含在内。\n\n");
            foreach(var s in report.summaries.Where(s=>s.target<0))text.Append("- ").Append(s.label).Append("：独占胜率 ").Append(P(s.winRate)).Append("；目标继续接触人数 ").Append(F(s.targetWilling)).Append("；直接推荐 ").Append(F(s.direct)).Append("；邻居分享 ").Append(F(s.shares)).Append("；拒绝 ").Append(F(s.rejected)).Append("（平静划走 ").Append(F(s.passed)).Append("，看腻 ").Append(F(s.overloaded)).Append("）。\n");
            text.Append("\n同开局配对差异（后者减前者；同种子的五目标先取均值，按 80 个开局分组计算 95% 描述区间）：\n\n");
            foreach(var c in report.pairedComparisons)text.Append("- ").Append(c.label).Append("：目标人数差 ").Append(F(c.targetCountDifference)).Append(" [").Append(F(c.targetCount95Lower)).Append(", ").Append(F(c.targetCount95Upper)).Append("]；胜率差 ").Append(P(c.winRateDifference)).Append(" [").Append(P(c.winRate95Lower)).Append(", ").Append(P(c.winRate95Upper)).Append("]；推荐次数差 ").Append(F(c.directDifference)).Append("；拒绝次数差 ").Append(F(c.rejectedDifference)).Append("。\n");
            text.Append("\n自适应参考会看真实兴趣、压力和个人阈值，用一次接触后的目标意愿相对其他主题的期望增益排序，可停推或换话题。它不预读随机结果，不代表玩家能达到这一表现。最终人数使用真实 WillingTopic 和 Scores，不使用预测值。\n\n");
            text.Append("局限：更少接触会改变共享反应随机流的后续抽取顺序；同种子控制了开局与独立分享计划，但没有给不同策略逐人配同一次反应骰。区间按 80 个开局分组作正态近似，避免把同种子的五目标当成独立开局；它仍不是人类样本或盲测证据。当前只比较一个固定早期发送时点；未覆盖夜晚才推、边观察边推等时机策略。每次都验证同操作可复现。\n\n");
            text.Append("建议盲测：同一开局各给 2—3 分钟，A 版保留当前完整卡，B 版显示活动、接收反应与粗略接触状态。记录玩家是否能解释“喜欢但划走”，是否主动停推/换对象、是否再次查看某住户；再考虑三次版本的节奏。测试组件不改正式玩法、不解锁成就、不接触玩家存档。\n\n原始逐局数据：TargetChoiceTrials.csv；分目标统计：TargetChoiceSummary.csv；完整结构数据：TargetChoiceReport.json；可复现及公平条件：TargetChoiceChecks.txt。\n");
            return text.ToString();
        }
        static void WriteCsv(Report report)
        {
            var trials=new StringBuilder("seed,target,policy,target_won,target_willing,target_activities,target_legacy_only,direct,shares,target_shares,passed,overloaded,rejected,switched_topic_direct,changed_activity_by_share,official_opportunities,max_direct_in_stage,opening_fingerprint,result_fingerprint\n");
            foreach(var r in report.trials)trials.AppendLine(string.Join(",",r.seed,r.target,r.policy,r.targetWon?1:0,r.targetWilling,r.targetActivities,r.targetLegacyOnly,r.direct,r.shares,r.targetShares,r.passed,r.overloaded,r.rejected,r.switchedTopicDirect,r.changedActivityByShare,r.availableDirectOpportunities,r.maxDirectInStage,r.openingFingerprint,r.resultFingerprint));
            var summaries=new StringBuilder("policy,target,trials,win_rate,target_willing,target_activities,target_legacy_only,direct,shares,target_shares,passed,overloaded,rejected,target_willing_per_direct\n");
            foreach(var s in report.summaries)summaries.AppendLine(string.Join(",",s.policy,s.target,s.trials,R(s.winRate),R(s.targetWilling),R(s.targetActivities),R(s.targetLegacyOnly),R(s.direct),R(s.shares),R(s.targetShares),R(s.passed),R(s.overloaded),R(s.rejected),R(s.targetWillingPerDirect)));
            File.WriteAllText(Folder+"/TargetChoiceTrials.csv",trials.ToString(),new UTF8Encoding(true));
            File.WriteAllText(Folder+"/TargetChoiceSummary.csv",summaries.ToString(),new UTF8Encoding(true));
        }
        sealed class Fingerprint
        {
            ulong hash=14695981039346656037UL;
            public string Value=>hash.ToString("x16",Invariant);
            public void Add(int value) { unchecked { for(int i=0;i<4;i++) { hash^=(byte)(value>>(i*8));hash*=1099511628211UL; } } }
            public void Add(string value) { Add(value==null?-1:value.Length);if(value!=null)foreach(char c in value)Add(c); }
            public void Add(ResidentState r)
            {
                Add(r.number);Add(r.preferredTopic);Add(r.previousPreference);Add(r.lastTopic);Add(r.lastSource);Add(r.stableStages);Add((int)r.reactionKind);
                Add(r.recommended?1:0);Add(r.shared?1:0);Add(r.receivedShare?1:0);Add(r.reaction);
                for(int i=0;i<5;i++) { Add(r.interest[i].GetHashCode());Add(r.saturation[i].GetHashCode());Add(r.aversionThresholds[i].GetHashCode());Add(r.propsUntil[i]); }
            }
        }
        static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException("[TargetChoiceProbe] "+message); }
        static float Clamp(float value,float min=0,float max=1)=>Math.Max(min,Math.Min(max,value));
        static string R(float value)=>value.ToString("R",Invariant);
        static string F(float value)=>value.ToString("F2",Invariant);
        static string P(float value)=>(value*100).ToString("F1",Invariant)+"%";
    }
}
#endif
