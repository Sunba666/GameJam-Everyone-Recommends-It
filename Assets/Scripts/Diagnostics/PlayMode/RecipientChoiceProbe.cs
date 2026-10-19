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
    // Offline matched counterfactual. Only direct recipients change; topic order/count/time are frozen.
    public sealed class RecipientChoiceProbe : MonoBehaviour
    {
        const string Folder="Builds/Verification/CreativeBeats";
        const int Seeds=80,Residents=9,Stages=12;
        const float Duration=90,ActionTime=8;
        static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        [Serializable] public sealed class StagePlan
        {
            public int stage,tick=80;
            public int[] topics,controlResidents,randomResidents;
        }
        [Serializable] public sealed class Outcome
        {
            public bool targetWon;
            public int targetWilling,direct,shares,passed,overloaded,directOverloaded,directRejected;
            public int availableOpportunities,maxDirectInStage;
            public string fingerprint;
        }
        [Serializable] public sealed class Pair
        {
            public int seed,target,changedRecipients;
            public bool matchedDirectProtocol,identicalOpeningAndTutorial;
            public Outcome control,randomRecipients;
            public StagePlan[] stages;
        }
        [Serializable] public sealed class Summary
        {
            public string strategy;
            public int target=-1,runs;
            public float winRate,targetWilling,direct,shares,passed,overloaded,directOverloaded,directRejected;
        }
        [Serializable] public sealed class Difference
        {
            public int pairs,seedBlocks,controlHigherWilling,randomHigherWilling,equalWilling,controlOnlyWins,randomOnlyWins;
            public float targetCountDifference,targetCount95Lower,targetCount95Upper;
            public float winRateDifference,winRate95Lower,winRate95Upper,overloadedDifference,shareDifference;
        }
        [Serializable] sealed class Report
        {
            public string completedUtc,method,limitations,interpretation;
            public int pairs,seeds=Seeds,targets=5,primaryRuns,reproducedControlRuns,reproducedRandomRuns,totalRuns;
            public float elapsedRealSeconds,actionAtSeconds=ActionTime,stageSeconds=Duration;
            public bool sameOpeningTutorialTopicsCountsTimes,equalOfficialOpportunityRules,readOnlyLocalSimulation;
            public SimulationParameters parameters;
            public Summary[] summaries;
            public Difference difference;
            public Pair[] pairedTrials;
        }
        struct Choice { public int resident,topic;public float gain; }
        sealed class Evidence
        {
            public Outcome outcome=new Outcome();public Digest digest=new Digest();
            public string opening;public readonly List<Delivery> protocol=new List<Delivery>();
        }
        struct Delivery { public int stage,tick,topic; }

        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);var watch=System.Diagnostics.Stopwatch.StartNew();
            File.WriteAllText(Folder+"/RecipientChoiceChecks.txt","RUNNING: no conclusion until this execution writes COMPLETE.\nstarted_utc="+DateTime.UtcNow.ToString("O",Invariant));
            var pairs=new List<Pair>();int reproductions=0;bool complete=false;
            try
            {
                for(int seed=1;seed<=Seeds;seed++)for(int target=0;target<5;target++)
                {
                    var plans=new List<StagePlan>();var control=Control(seed,target,plans);
                    var random=ReplayPlan(seed,target,plans,true);
                    Require(control.opening==random.opening,"Same opening and same resident-301 teaching result");
                    Require(SameProtocol(control.protocol,random.protocol),"Every direct topic, order, count and timestamp matches");
                    Require(control.outcome.direct==random.outcome.direct,"Total direct volume matches");
                    Require(control.outcome.availableOpportunities==108&&random.outcome.availableOpportunities==108,"Official 108 opportunities are unchanged in both private worlds");
                    Require(control.outcome.maxDirectInStage<=3&&random.outcome.maxDirectInStage<=3,"Both experimental schedules include teaching within stage one's three slots");
                    int changed=plans.Sum(p=>p.controlResidents.Zip(p.randomResidents,(a,b)=>a==b?0:1).Sum());
                    pairs.Add(new Pair { seed=seed,target=target,control=control.outcome,randomRecipients=random.outcome,stages=plans.ToArray(),changedRecipients=changed,matchedDirectProtocol=true,identicalOpeningAndTutorial=true });
                    // One target per opening, rotating evenly over all five: 80 genuine reproducibility
                    // checks for both the original recorded plan and randomized recipient replay.
                    if(target==(seed-1)%5)
                    {
                        var originalReplay=ReplayPlan(seed,target,plans,false);var randomReplay=ReplayPlan(seed,target,plans,true);
                        Require(originalReplay.outcome.fingerprint==control.outcome.fingerprint,"Replaying the fixed control plan reproduces its real result");
                        Require(randomReplay.outcome.fingerprint==random.outcome.fingerprint,"Random-recipient replay reproduces with the same independent recipient seed");
                        Require(SameProtocol(control.protocol,originalReplay.protocol)&&SameProtocol(random.protocol,randomReplay.protocol),"Repeated plans retain matching direct protocol");
                        reproductions++;
                    }
                    File.WriteAllText(Folder+"/RecipientChoiceProgress.txt","RUNNING "+pairs.Count+" / 400 pairs\nseed="+seed+" target="+target+"\nseconds="+watch.Elapsed.TotalSeconds.ToString("F1",Invariant));
                    yield return null;
                }
                var summaries=new List<Summary>();
                foreach(bool control in new[]{true,false})
                {
                    summaries.Add(Aggregate(pairs,control,-1));
                    for(int target=0;target<5;target++)summaries.Add(Aggregate(pairs.Where(p=>p.target==target),control,target));
                }
                var difference=Compare(pairs);string interpretation=Interpret(difference);
                var report=new Report {
                    completedUtc=DateTime.UtcNow.ToString("O",Invariant),pairs=pairs.Count,primaryRuns=pairs.Count*2,
                    reproducedControlRuns=reproductions,reproducedRandomRuns=reproductions,totalRuns=pairs.Count*2+reproductions*2,
                    elapsedRealSeconds=(float)watch.Elapsed.TotalSeconds,sameOpeningTutorialTopicsCountsTimes=true,
                    equalOfficialOpportunityRules=true,readOnlyLocalSimulation=true,parameters=new SimulationParameters(),
                    summaries=summaries.ToArray(),difference=difference,pairedTrials=pairs.ToArray(),interpretation=interpretation,
                    method="80 common seeds x 5 targets = 400 pairs. First complete an AdaptiveThree control run and record its full 12-stage direct recommendation plan. Recreate the same ResidentRun opening and resident-301 target tutorial at tick 0; replay the control's exact per-stage topic order/count at tick 80. Only recipients change, using an independent seeded shuffle of residents whose direct opportunity is unused. Random recipient selection reads no interest, pressure or thresholds. All original recovery/sharing/reaction rules run normally. One target per seed (80 pairs, evenly rotating targets) replays both plans for determinism.",
                    limitations="The topic/count plan comes from the hidden-state control world's future and is held fixed even when the randomized world develops differently. This is an offline matched counterfactual, not an adaptive or player-visible strategy. It estimates recipient effects conditional on this control policy's plans, not all possible plans. Endogenous neighbour sharing is allowed to diverge; it is a downstream consequence, not forced to match. Receive still uses the genuine common random stream: different neighbour counts can change later reaction-roll assignment. Paired 95% descriptive intervals average five targets within each of 80 seed blocks. No human playtest, live scene, live run, PlayerPrefs or official cap is involved."
                };
                WriteCsv(report);
                File.WriteAllText(Folder+"/RecipientChoiceReport.json",JsonUtility.ToJson(report,true),new UTF8Encoding(false));
                File.WriteAllText(Folder+"/RecipientChoiceConclusion.md",Conclusion(report),new UTF8Encoding(false));
                File.WriteAllLines(Folder+"/RecipientChoiceChecks.txt",new[] {
                    "PASS 400 matched recipient pairs: 80 genuine openings x every target; 800 primary ResidentRun runs",
                    "PASS Same opening and tutorial; every direct stage, tick, topic order and count matches exactly",
                    "PASS Official opportunities remain 108; teaching included in stage-one experimental three-person budget",
                    "PASS Independent random recipient shuffle reads only unused direct opportunities, never hidden state",
                    "PASS 80 recorded control plans and 80 randomized plans rerun identically; 960 total genuine runs",
                    "PASS Every run settles real recovery and sharing, uses actual willingness standings, and stops at stage 12",
                    "PASS No live-run, scene or PlayerPrefs/save access; prior TargetChoice outputs not overwritten",
                    "COMPLETE "+report.completedUtc
                },new UTF8Encoding(false));
                File.WriteAllText(Folder+"/RecipientChoiceProgress.txt","COMPLETE "+report.completedUtc+"\n"+interpretation+"\nseconds="+watch.Elapsed.TotalSeconds.ToString("F1",Invariant));
                complete=true;Debug.Log("[RecipientChoiceProbe] 400 matched pairs complete. "+interpretation);
            }
            finally { if(!complete)File.WriteAllText(Folder+"/RecipientChoiceChecks.txt","FAILED OR INTERRUPTED; partial progress is not a comparison result.\ncompleted_pairs="+pairs.Count); }
            Destroy(this);
        }
        static ResidentRun Begin(int seed,int target,Evidence evidence)
        {
            var run=new ResidentRun(Residents,Duration,Stages,seed);
            Require(run.SetTarget(target)&&run.Recommend(0,target)&&run.StartStage(),"Common target/tutorial/start accepted once");
            var digest=new Digest();digest.Add(seed);digest.Add(target);
            foreach(var resident in run.opening)digest.Add(resident);
            foreach(var resident in run.residents)digest.Add(resident);
            evidence.opening=digest.Value;evidence.digest.Add(evidence.opening);
            evidence.outcome.availableOpportunities=run.Available+1;
            return run;
        }
        static Evidence Control(int seed,int target,List<StagePlan> plans)
        {
            var evidence=new Evidence();var run=Begin(seed,target,evidence);
            while(true)
            {
                run.Tick(ActionTime);var choices=new List<Choice>();
                for(int resident=0;resident<Residents;resident++)
                {
                    if(run.residents[resident].recommended)continue;
                    var state=run.residents[resident];float before=Utility(state,target,-1,0,run.rules);
                    var best=new Choice { resident=resident,topic=-1,gain=.005f };
                    for(int topic=0;topic<5;topic++)
                    {
                        float gain=ExpectedUtility(state,target,topic,run.rules)-before;
                        if(gain>best.gain+.000001f)best=new Choice { resident=resident,topic=topic,gain=gain };
                    }
                    if(best.topic>=0)choices.Add(best);
                }
                choices.Sort((a,b)=>{int comparison=b.gain.CompareTo(a.gain);return comparison==0?a.resident.CompareTo(b.resident):comparison;});
                var chosen=choices.Take(Math.Max(0,3-run.residents.Count(r=>r.recommended))).ToArray();
                var plan=new StagePlan { stage=run.Stage,topics=chosen.Select(c=>c.topic).ToArray(),controlResidents=chosen.Select(c=>c.resident).ToArray() };plans.Add(plan);
                foreach(var choice in chosen)Require(run.Recommend(choice.resident,choice.topic),"Control opportunity is valid");
                if(SettleAndAdvance(run,evidence))break;
            }
            Complete(run,evidence);return evidence;
        }
        static Evidence ReplayPlan(int seed,int target,List<StagePlan> plans,bool randomRecipients)
        {
            var evidence=new Evidence();var run=Begin(seed,target,evidence);
            foreach(var plan in plans)
            {
                Require(run.Stage==plan.stage&&!run.StageEnded,"Frozen plan stage aligns with actual counterfactual stage");run.Tick(ActionTime);
                int[] recipients=plan.controlResidents;
                if(randomRecipients)
                {
                    recipients=Enumerable.Range(0,Residents).Where(i=>!run.residents[i].recommended).ToArray();
                    // The only selection input is the unused-opportunity flag. No hidden preference,
                    // interest, saturation or threshold determines recipients or shuffle order.
                    var recipientRandom=new System.Random(unchecked(seed*1103515245^target*486187739^plan.stage*104729^0x49C4C8AB));
                    for(int i=recipients.Length-1;i>0;i--) { int other=recipientRandom.Next(i+1),swap=recipients[i];recipients[i]=recipients[other];recipients[other]=swap; }
                    recipients=recipients.Take(plan.topics.Length).ToArray();
                    if(plan.randomResidents!=null)Require(plan.randomResidents.SequenceEqual(recipients),"Recipient plan repeats independently");
                    else plan.randomResidents=recipients;
                }
                Require(recipients.Length==plan.topics.Length,"Each frozen topic has exactly one eligible recipient");
                for(int i=0;i<plan.topics.Length;i++)Require(run.Recommend(recipients[i],plan.topics[i]),"Frozen-topic submission accepted exactly once at the matching time");
                bool final=SettleAndAdvance(run,evidence);Require(final==(plan.stage==Stages),"Frozen plan and final-stage boundary agree");
            }
            Complete(run,evidence);return evidence;
        }
        static bool SettleAndAdvance(ResidentRun run,Evidence evidence)
        {
            Require(Math.Abs(run.Elapsed-ActionTime)<.001f,"Every post-teaching player action occurs at tick 80");
            run.FinishEarly();Require(run.StageEnded&&run.Remaining==0,"Every real remaining tick and share attempt settles");
            evidence.outcome.maxDirectInStage=Math.Max(evidence.outcome.maxDirectInStage,run.residents.Count(r=>r.recommended));
            foreach(var e in run.events)
            {
                evidence.digest.Add(e.stage);evidence.digest.Add(e.tick);evidence.digest.Add(e.resident);evidence.digest.Add(e.topic);evidence.digest.Add(e.source);evidence.digest.Add(e.kind);evidence.digest.Add((int)e.reaction);evidence.digest.Add(e.previousTopic);evidence.digest.Add(e.currentTopic);
                if(e.resident>=0&&e.after!=null)evidence.digest.Add(e.after[e.resident]);
                bool direct=e.kind=="玩家推荐",share=e.kind=="邻居分享";if(!direct&&!share)continue;
                if(direct)
                {
                    evidence.outcome.direct++;evidence.protocol.Add(new Delivery { stage=e.stage,tick=e.tick,topic=e.topic });
                    if(e.reaction==ResidentReaction.Overloaded)evidence.outcome.directOverloaded++;
                    if(e.reaction==ResidentReaction.Overloaded||e.reaction==ResidentReaction.Passed)evidence.outcome.directRejected++;
                }
                if(share)evidence.outcome.shares++;
                if(e.reaction==ResidentReaction.Overloaded)evidence.outcome.overloaded++;
                if(e.reaction==ResidentReaction.Passed)evidence.outcome.passed++;
            }
            foreach(var resident in run.residents)evidence.digest.Add(resident);
            run.events.Clear(); // Only this private diagnostic's already digested record list.
            if(run.Finished)return true;
            Require(run.NextStage(),"A settled nonfinal stage advances once");evidence.outcome.availableOpportunities+=run.Available;return false;
        }
        static void Complete(ResidentRun run,Evidence evidence)
        {
            Require(run.Finished&&run.Stage==Stages&&!run.NextStage(),"Real final standings; no stage 13");
            evidence.outcome.targetWon=run.TargetWon();evidence.outcome.targetWilling=run.Scores()[run.Target];
            evidence.digest.Add(evidence.outcome.targetWon?1:0);foreach(int score in run.Scores())evidence.digest.Add(score);
            evidence.outcome.fingerprint=evidence.digest.Value;
        }
        static bool SameProtocol(List<Delivery> a,List<Delivery> b)=>a.Count==b.Count&&a.Zip(b,(x,y)=>x.stage==y.stage&&x.tick==y.tick&&x.topic==y.topic).All(same=>same);
        // Same deterministic developer reference used by TargetChoiceProbe.AdaptiveThree.
        static float ExpectedUtility(ResidentState resident,int target,int delivered,SimulationParameters rules)
        {
            float chance=Clamp(.2f+resident.interest[delivered]*.67f-resident.PressureRatio(delivered)*.38f,.05f,.88f);
            if(Clamp(resident.saturation[delivered]+rules.pressureGain)/Math.Max(.1f,resident.aversionThresholds[delivered])>=1)return Utility(resident,target,delivered,-.015f,rules);
            float curious=Math.Min(.22f,1-chance);return chance*Utility(resident,target,delivered,rules.interestGain,rules)+curious*Utility(resident,target,delivered,.065f,rules)+(1-chance-curious)*Utility(resident,target,delivered,0,rules);
        }
        static float Utility(ResidentState resident,int target,int delivered,float delta,SimulationParameters rules)
        {
            float targetScore=0,competitor=0;
            for(int topic=0;topic<5;topic++)
            {
                float interest=resident.interest[topic],pressure=resident.saturation[topic];
                if(delivered>=0) { pressure=Clamp(pressure+(topic==delivered?rules.pressureGain:-rules.otherTopicRelief));if(topic==delivered)interest=Clamp(interest+delta); }
                float score=interest*(1-.78f*Math.Min(1,pressure/Math.Max(.1f,resident.aversionThresholds[topic])));
                if(topic==target)targetScore=score;else competitor=Math.Max(competitor,score);
            }
            return Clamp(.5f+(targetScore-competitor)/.24f);
        }
        static Summary Aggregate(IEnumerable<Pair> source,bool control,int target)
        {
            var outcomes=source.Select(p=>control?p.control:p.randomRecipients).ToArray();return new Summary {
                strategy=control?"按隐藏状态选对象的三人控制":"固定控制计划、随机换对象",target=target,runs=outcomes.Length,
                winRate=(float)outcomes.Average(o=>o.targetWon?1d:0d),targetWilling=(float)outcomes.Average(o=>o.targetWilling),direct=(float)outcomes.Average(o=>o.direct),shares=(float)outcomes.Average(o=>o.shares),
                passed=(float)outcomes.Average(o=>o.passed),overloaded=(float)outcomes.Average(o=>o.overloaded),directOverloaded=(float)outcomes.Average(o=>o.directOverloaded),directRejected=(float)outcomes.Average(o=>o.directRejected) };
        }
        static Difference Compare(List<Pair> pairs)
        {
            var blocks=pairs.GroupBy(p=>p.seed).Select(g=>new { count=g.Average(p=>p.control.targetWilling-p.randomRecipients.targetWilling),win=g.Average(p=>(p.control.targetWon?1d:0d)-(p.randomRecipients.targetWon?1d:0d)) }).ToArray();
            var result=new Difference { pairs=pairs.Count,seedBlocks=blocks.Length,targetCountDifference=(float)blocks.Average(b=>b.count),winRateDifference=(float)blocks.Average(b=>b.win),
                controlHigherWilling=pairs.Count(p=>p.control.targetWilling>p.randomRecipients.targetWilling),randomHigherWilling=pairs.Count(p=>p.control.targetWilling<p.randomRecipients.targetWilling),equalWilling=pairs.Count(p=>p.control.targetWilling==p.randomRecipients.targetWilling),
                controlOnlyWins=pairs.Count(p=>p.control.targetWon&&!p.randomRecipients.targetWon),randomOnlyWins=pairs.Count(p=>!p.control.targetWon&&p.randomRecipients.targetWon),overloadedDifference=(float)pairs.Average(p=>p.control.overloaded-p.randomRecipients.overloaded),shareDifference=(float)pairs.Average(p=>p.control.shares-p.randomRecipients.shares) };
            float countRadius=Radius(blocks.Select(b=>b.count).ToArray()),winRadius=Radius(blocks.Select(b=>b.win).ToArray());
            result.targetCount95Lower=result.targetCountDifference-countRadius;result.targetCount95Upper=result.targetCountDifference+countRadius;result.winRate95Lower=result.winRateDifference-winRadius;result.winRate95Upper=result.winRateDifference+winRadius;return result;
        }
        static float Radius(double[] values) { double mean=values.Average();return (float)(1.96*Math.Sqrt(values.Sum(v=>(v-mean)*(v-mean))/(values.Length-1)/values.Length)); }
        static string Interpret(Difference d)=>d.targetCount95Lower>0||d.winRate95Lower>0
            ?"固定发送次数、主题顺序与虚拟时刻后，按状态选对象仍产生正向差异；本批原模拟支持对象选择有价值。结果限于这套控制计划，不等于玩家已经能看懂取舍。"
            :d.targetCountDifference>0||d.winRateDifference>0
                ?"固定次数、主题与时刻后的对象对照仅出现平均优势，描述区间跨零；不足以证明稳定的对象收益，不能用原全推对比代替该证据。"
                :"固定次数、主题与时刻后，这套按状态选对象参考没有优于随机对象；此前频次和话题的综合差异不能证明对象选择收益。";
        static string Conclusion(Report report)
        {
            var text=new StringBuilder("# 固定投递计划，只改变推荐对象\n\n");text.Append(report.interpretation).Append("\n\n");
            text.Append("80 开局 × 五目标，共 400 对；800 次主要原模拟运行，加 80 组两种计划复现，共 960 次；完成 UTC：").Append(report.completedUtc).Append("；真实耗时 ").Append(F(report.elapsedRealSeconds)).Append(" 秒。\n\n");
            foreach(var s in report.summaries.Where(s=>s.target<0))text.Append("- ").Append(s.strategy).Append("：目标独占胜率 ").Append(P(s.winRate)).Append("；真实目标意愿人数 ").Append(F(s.targetWilling)).Append("；直接推荐 ").Append(F(s.direct)).Append("；邻居分享 ").Append(F(s.shares)).Append("；过载 ").Append(F(s.overloaded)).Append("（玩家投递造成 ").Append(F(s.directOverloaded)).Append("）。\n");
            var d=report.difference;text.Append("\n控制减随机：目标人数差 ").Append(F(d.targetCountDifference)).Append(" [").Append(F(d.targetCount95Lower)).Append(", ").Append(F(d.targetCount95Upper)).Append("]；胜率差 ").Append(P(d.winRateDifference)).Append(" [").Append(P(d.winRate95Lower)).Append(", ").Append(P(d.winRate95Upper)).Append("]。同开局五目标先平均，按 80 个 seed 分组计算正态近似 95% 描述区间。\n\n");
            text.Append("匹配条件：同九人开局、同 301 教学、第 8 秒时刻、每阶段推荐数量与主题顺序逐项相同；只有对象来自独立随机洗牌。教学计入第一阶段三次实验预算，正式规则仍有 108 次机会。记录每阶段控制/随机对象名单供逐项审查。\n\n");
            text.Append("解释边界：完整控制局的未来投递计划先记录，反事实局即使发展不同仍沿用它；这是离线对照，不是玩家可见策略。控制对象能读隐藏兴趣/压力/阈值，随机对象仅看有没有机会。自然邻居分享不强行匹配，因为它是改投对象后的真实后果；分享次数差会改变之后共用接收随机流的分配。本结果只估计这套控制计划下的对象差异，不能证明所有发送计划、玩家认知或限额体验。\n\n");
            text.Append("原始数据：RecipientChoiceTrials.csv；严格投递匹配明细：RecipientChoiceProtocol.csv；按主题统计：RecipientChoiceSummary.csv；结构报告：RecipientChoiceReport.json；完成证据：RecipientChoiceChecks.txt。原 1600 局报告保留，未重跑或覆盖。\n");return text.ToString();
        }
        static void WriteCsv(Report report)
        {
            var rows=new StringBuilder("seed,target,control_won,random_won,control_willing,random_willing,direct_both,control_shares,random_shares,control_overloaded,random_overloaded,control_direct_overloaded,random_direct_overloaded,changed_recipients,control_fingerprint,random_fingerprint\n");
            var protocol=new StringBuilder("seed,target,stage,tick,topics_in_order,control_recipients_in_order,random_recipients_in_order\n");
            foreach(var pair in report.pairedTrials)
            {
                var a=pair.control;var b=pair.randomRecipients;rows.AppendLine(string.Join(",",pair.seed,pair.target,a.targetWon?1:0,b.targetWon?1:0,a.targetWilling,b.targetWilling,a.direct,a.shares,b.shares,a.overloaded,b.overloaded,a.directOverloaded,b.directOverloaded,pair.changedRecipients,a.fingerprint,b.fingerprint));
                protocol.AppendLine(string.Join(",",pair.seed,pair.target,1,0,pair.target,0,0));
                foreach(var plan in pair.stages)protocol.AppendLine(string.Join(",",pair.seed,pair.target,plan.stage,plan.tick,string.Join("|",plan.topics),string.Join("|",plan.controlResidents),string.Join("|",plan.randomResidents)));
            }
            var summary=new StringBuilder("control,target,runs,win_rate,target_willing,direct,shares,passed,overloaded,direct_overloaded,direct_rejected\n");
            foreach(var s in report.summaries)summary.AppendLine(string.Join(",",s.strategy.StartsWith("按")?1:0,s.target,s.runs,R(s.winRate),R(s.targetWilling),R(s.direct),R(s.shares),R(s.passed),R(s.overloaded),R(s.directOverloaded),R(s.directRejected)));
            File.WriteAllText(Folder+"/RecipientChoiceTrials.csv",rows.ToString(),new UTF8Encoding(true));File.WriteAllText(Folder+"/RecipientChoiceProtocol.csv",protocol.ToString(),new UTF8Encoding(true));File.WriteAllText(Folder+"/RecipientChoiceSummary.csv",summary.ToString(),new UTF8Encoding(true));
        }
        sealed class Digest
        {
            ulong value=14695981039346656037UL;public string Value=>value.ToString("x16",Invariant);
            public void Add(int number) { unchecked { for(int i=0;i<4;i++) { value^=(byte)(number>>(i*8));value*=1099511628211UL; } } }
            public void Add(string text) { Add(text==null?-1:text.Length);if(text!=null)foreach(char c in text)Add(c); }
            public void Add(ResidentState r)
            {
                Add(r.number);Add(r.preferredTopic);Add(r.previousPreference);Add(r.lastTopic);Add(r.lastSource);Add(r.stableStages);Add((int)r.reactionKind);Add(r.recommended?1:0);Add(r.shared?1:0);Add(r.receivedShare?1:0);Add(r.reaction);
                for(int i=0;i<5;i++) { Add(r.interest[i].GetHashCode());Add(r.saturation[i].GetHashCode());Add(r.aversionThresholds[i].GetHashCode());Add(r.propsUntil[i]); }
            }
        }
        static void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException("[RecipientChoiceProbe] "+message); }
        static float Clamp(float value,float min=0,float max=1)=>Math.Max(min,Math.Min(max,value));
        static string R(float value)=>value.ToString("R",Invariant);
        static string F(float value)=>value.ToString("F2",Invariant);
        static string P(float value)=>(value*100).ToString("F1",Invariant)+"%";
    }
}
#endif
