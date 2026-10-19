using System;
using System.Collections.Generic;
using System.Linq;

namespace EveryoneRecommends.Frontend
{
    [Serializable]
    public sealed class SimulationParameters
    {
        public float interestGain=.22f, pressureGain=.2f, neighbourPressureGain=.14f;
        public float recoveryPerSecond=.0008f, otherTopicRelief=.05f;
        public float shareBaseChance=.38f, aversionThreshold=.7f;
        public float thresholdVariation=.18f;
        public float daySeconds=45, duskSeconds=3;
        public float shareWindowStart=.18f, shareWindowEnd=.90f, shareMinimumGap=2.5f;
    }
    public enum ResidentReaction { Quiet, Curious, Happy, Passed, Overloaded }
    [Serializable]
    public sealed class ResidentState
    {
        public int number, preferredTopic, lastTopic=-1, lastSource=-2, stableStages;
        public int previousPreference=-1;
        public float[] interest=new float[5], saturation=new float[5];
        public float[] aversionThresholds={.7f,.7f,.7f,.7f,.7f};
        public ResidentReaction reactionKind;
        public int[] propsUntil=new int[5];
        public bool recommended, shared, receivedShare;
        public string reaction="正在做自己喜欢的事";
        public const float AversionThreshold=.7f;
        public float PressureRatio(int topic)=>saturation[topic]/Math.Max(.1f,aversionThresholds[topic]);
        public int Attitude(int topic)=>PressureRatio(topic)>=1?2:interest[topic]>=.5f?0:1;
        public ResidentState Copy()
        {
            var copy=(ResidentState)MemberwiseClone();
            copy.interest=(float[])interest.Clone(); copy.saturation=(float[])saturation.Clone(); copy.aversionThresholds=(float[])aversionThresholds.Clone();copy.propsUntil=(int[])propsUntil.Clone(); return copy;
        }
        public int WillingTopic()
        {
            int best=0; float score=-1;
            for(int i=0;i<5;i++) { float value=interest[i]*(1-.78f*Math.Min(1,PressureRatio(i))); if(value>score) { best=i;score=value; } }
            return best;
        }
    }
    [Serializable]
    public sealed class RunEvent
    {
        public int stage,tick,resident,topic,source;
        public string kind,text;
        public int previousTopic=-1,currentTopic=-1;
        public ResidentReaction reaction;
        public ResidentState[] after;
    }
    public sealed class ResidentRun
    {
        public static readonly string[] TopicNames={"猫","美食","游戏","新闻","音乐"};
        public readonly ResidentState[] residents;
        public readonly ResidentState[] opening;
        public readonly List<RunEvent> events=new List<RunEvent>();
        public readonly int seed,totalStages;
        public readonly float stageDuration;
        public readonly SimulationParameters rules;
        readonly Random random;
        ResidentState[] shareOpening;
        ResidentShareSchedule shareSchedule;
        int tick;
        double accumulator;
        bool duskDone;
        public int Stage { get; private set; }=1;
        public int Target { get; private set; }=-1;
        public bool TutorialDone { get; private set; }
        public bool Started { get; private set; }
        public bool StageEnded { get; private set; }
        public bool Finished=>StageEnded&&Stage==totalStages;
        public float Elapsed=>tick*.1f;
        public float Remaining=>Math.Max(0,stageDuration-Elapsed);
        public string TimeOfDay=>Elapsed<rules.daySeconds?"白天":Elapsed<rules.daySeconds+rules.duskSeconds?"黄昏":"晚上";
        public int Available=>residents.Count(r=>!r.recommended);
        public ResidentRun(int count,float duration,int stages,int openingSeed,SimulationParameters parameters=null)
        {
            if(count<1||duration<=0||stages<1)throw new ArgumentOutOfRangeException();
            seed=openingSeed;stageDuration=duration;totalStages=stages;rules=parameters??new SimulationParameters();
            random=new Random(seed); residents=new ResidentState[count];
            var thresholdRandom=new Random(unchecked(seed^0x51C0A7));
            for(int i=0;i<count;i++)
            {
                var r=new ResidentState{number=i+1,preferredTopic=random.Next(5)};
                for(int t=0;t<5;t++) { r.interest[t]=.12f+(float)random.NextDouble()*.36f; r.saturation[t]=(float)random.NextDouble()*.16f; }
                for(int t=0;t<5;t++)r.aversionThresholds[t]=Clamp(rules.aversionThreshold+((float)thresholdRandom.NextDouble()*2-1)*rules.thresholdVariation,.25f,.95f);
                r.interest[r.preferredTopic]=.6f+(float)random.NextDouble()*.18f; r.previousPreference=r.preferredTopic;
                residents[i]=r;
            }
            opening=Snapshot();shareOpening=Snapshot();
            shareSchedule=new ResidentShareSchedule(seed,Stage,residents.Length,stageDuration,rules);
        }
        public static int FloorOf(int i)=>i<3?3:i<6?2:1;
        public static string RoomOf(int i)=>(FloorOf(i)*100+(i<3?i+1:i<6?i-2:i-5)).ToString();
        public bool SetTarget(int topic)
        {
            if(Started||Target>=0||topic<0||topic>=5)return false;
            Target=topic;return true;
        }
        public bool StartStage()
        {
            if(Started||Target<0||!TutorialDone||StageEnded)return false;
            Started=true;return true;
        }
        public bool Recommend(int index,int topic)
        {
            if(StageEnded||Target<0||index<0||index>=residents.Length||topic<0||topic>=5||residents[index].recommended||(!Started&&TutorialDone))return false;
            residents[index].recommended=true;
            Receive(index,topic,-1,"玩家推荐");
            if(!Started)TutorialDone=true;
            return true;
        }
        void Receive(int index,int topic,int source,string kind)
        {
            var r=residents[index];float oldInterest=r.interest[topic];int activityBefore=r.preferredTopic;
            float chance=Clamp(.2f+oldInterest*.67f-r.PressureRatio(topic)*.38f,.05f,.88f);
            double roll=random.NextDouble();
            r.saturation[topic]=Clamp(r.saturation[topic]+(source==-1?rules.pressureGain:rules.neighbourPressureGain));
            for(int t=0;t<5;t++)if(t!=topic)r.saturation[t]=Clamp(r.saturation[t]-rules.otherTopicRelief);
            if(r.PressureRatio(topic)>=1)
            {
                r.interest[topic]=Clamp(oldInterest-.015f);r.reactionKind=ResidentReaction.Overloaded;
                r.reaction=oldInterest>=.6f&&activityBefore==topic?"喜欢归喜欢，今天先不刷了":"又是这个……先划走";
            }
            else if(roll<chance)
            {
                r.interest[topic]=Clamp(oldInterest+rules.interestGain);r.reaction="看得很开心，想再了解一点";r.reactionKind=ResidentReaction.Happy;
            }
            else if(roll<chance+.22)
            {
                r.interest[topic]=Clamp(oldInterest+.065f);r.reaction="认真看了几眼，有点好奇";r.reactionKind=ResidentReaction.Curious;
            }
            else { r.reaction="看了一眼，平静地划走";r.reactionKind=ResidentReaction.Passed; }
            int preferred=r.preferredTopic;
            for(int t=0;t<5;t++)if(r.interest[t]>r.interest[preferred]+.025f)preferred=t;
            r.preferredTopic=preferred;r.lastTopic=topic;r.lastSource=source;
            AddEvent(index,topic,source,kind,RoomOf(index)+" 收到「"+TopicNames[topic]+"」 · "+r.reaction+(preferred!=activityBefore?"；开始接触「"+TopicNames[preferred]+"」":""),activityBefore);
        }
        public void Tick(float seconds)
        {
            if(!Started||StageEnded||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            accumulator+=seconds;
            while(accumulator>=.099999 && !StageEnded)
            {
                accumulator-=.1;tick++;
                foreach(var r in residents)for(int t=0;t<5;t++)r.saturation[t]=Clamp(r.saturation[t]-rules.recoveryPerSecond*.1f);
                if(!duskDone&&Elapsed+0.0001f>=rules.daySeconds)
                { duskDone=true;AddEvent(-1,-1,-2,"黄昏","天色渐暖，楼里的灯慢慢亮起来"); }
                while(shareSchedule.TryTake(tick,out var attempt))TryShare(attempt);
                if(Elapsed+0.0001f>=stageDuration)
                {
                    StageEnded=true;accumulator=0;
                    foreach(var r in residents) { r.stableStages=r.preferredTopic==r.previousPreference?r.stableStages+1:1;r.previousPreference=r.preferredTopic; }
                    AddEvent(-1,-1,-2,"阶段结束","第 "+Stage+" 阶段结束，结果已保存");
                }
            }
        }
        void TryShare(ResidentShareSchedule.Attempt attempt)
        {
            // Topic eligibility uses the stage opening, never an instantly forwarded new topic.
            int sender=attempt.sender;if(residents[sender].shared)return;
            var initial=shareOpening[sender];int topic=initial.preferredTopic;
            float currentPressure=residents[sender].PressureRatio(topic);
            float chance=Clamp(rules.shareBaseChance+initial.interest[topic]*.35f-currentPressure*.35f,.03f,.85f);
            if(currentPressure>=1||initial.interest[topic]<.48f||attempt.acceptanceRoll>=chance)return;
            var candidates=new List<int>();
            for(int i=0;i<residents.Length;i++)if(i!=sender&&FloorOf(i)==FloorOf(sender)&&!residents[i].receivedShare)candidates.Add(i);
            if(candidates.Count==0)return;
            int receiver=candidates[Math.Min(candidates.Count-1,(int)(attempt.recipientRoll*candidates.Count))];
            residents[sender].shared=true;residents[receiver].receivedShare=true;
            Receive(receiver,topic,sender,"邻居分享");
        }
        public void FinishEarly() { if(Started&&!StageEnded)Tick(Remaining+.2f); }
        public bool NextStage()
        {
            if(!StageEnded||Finished)return false;
            Stage++;tick=0;accumulator=0;duskDone=false;
            foreach(var r in residents)r.recommended=r.shared=r.receivedShare=false;
            foreach(var r in residents)
            {
                if(r.stableStages>=2&&r.interest[r.preferredTopic]>=.62f)
                {
                    bool newly=r.propsUntil[r.preferredTopic]<Stage;
                    r.propsUntil[r.preferredTopic]=Stage+2;
                    if(newly)AddEvent(r.number-1,r.preferredTopic,-2,"生活变化",RoomOf(r.number-1)+" 的房间添了「"+TopicNames[r.preferredTopic]+"」用品");
                }
            }
            shareOpening=Snapshot();shareSchedule=new ResidentShareSchedule(seed,Stage,residents.Length,stageDuration,rules);
            StageEnded=false;Started=true;return true;
        }
        void AddEvent(int resident,int topic,int source,string kind,string text,int previousTopic=-1)
        {
            events.Add(new RunEvent{stage=Stage,tick=tick,resident=resident,topic=topic,source=source,kind=kind,text=text,previousTopic=previousTopic,
                currentTopic=resident>=0?residents[resident].preferredTopic:-1,reaction=resident>=0?residents[resident].reactionKind:ResidentReaction.Quiet,after=Snapshot()});
        }
        public string Reflection(int stage=0)
        {
            var contacts=events.Where(e=>(stage==0||e.stage==stage)&&(e.kind=="玩家推荐"||e.kind=="邻居分享")).ToArray();
            int direct=contacts.Count(e=>e.source==-1),shared=contacts.Length-direct;
            var paradox=contacts.LastOrDefault(e=>e.reaction==ResidentReaction.Overloaded&&e.currentTopic==e.topic);
            var change=contacts.LastOrDefault(e=>e.previousTopic>=0&&e.previousTopic!=e.currentTopic);
            string clue=paradox!=null?RoomOf(paradox.resident)+" 还在接触「"+TopicNames[paradox.topic]+"」，却划走了同主题推送。":
                change!=null?RoomOf(change.resident)+" 收到「"+TopicNames[change.topic]+"」后，开始接触「"+TopicNames[change.currentTopic]+"」。":
                shared>0?(direct==0?"这一阶段你没有直接推荐，邻居仍分享了 ":"这一阶段你推荐了 "+direct+" 次，邻居另外分享了 ")+shared+" 次内容。":direct==0?"这次选择了观察，没有直接推荐或邻居分享。":"这次没有发生邻居分享，可以在住户记录中比较接收反应。";
            return "你的推荐 "+direct+" 次 · 邻居分享 "+shared+" 次\n"+clue;
        }
        public ResidentState[] Snapshot()=>residents.Select(r=>r.Copy()).ToArray();
        public int[] Scores()
        {
            var scores=new int[5];foreach(var r in residents)scores[r.WillingTopic()]++;return scores;
        }
        public bool TargetWon()
        {
            if(!Finished||Target<0)return false;var scores=Scores();int max=scores.Max();return scores[Target]==max&&scores.Count(x=>x==max)==1;
        }
        public EndingId Ending()
        {
            if(!Finished)return EndingId.None;
            var scores=Scores();int max=scores.Max();
            return scores.Count(value=>value==max)>1?EndingId.Balanced:(EndingId)Array.IndexOf(scores,max);
        }
        public string SourceName(int source)=>source==-1?"来自玩家":source>=0?"来自邻居 "+RoomOf(source):"生活记录";
        static float Clamp(float value,float min=0,float max=1)=>Math.Max(min,Math.Min(max,value));
    }
}
