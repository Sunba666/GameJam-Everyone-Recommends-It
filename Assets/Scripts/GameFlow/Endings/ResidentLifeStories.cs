using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EveryoneRecommends.Frontend
{
    // A wish is character context, never a simulation objective or a completion flag.
    // Every outcome below reads the supplied state/event record without changing it.
    public static class ResidentLifeStories
    {
        public static readonly int[] FocusResidents={0,4,6};
        static readonly string[] Activities={"看猫内容","吃东西","打游戏","读报","听音乐"};

        public static string Name(int index)
        {
            switch(index) { case 0:return "阿团";case 4:return "小音";case 6:return "阿宁";default:return "住户 "+ResidentRun.RoomOf(index); }
        }
        public static string FocusLabel(int index)=>GetWishTopic(index)<0?Name(index):ResidentRun.RoomOf(index)+" · "+Name(index);
        public static string GetWish(int index)
        {
            switch(index)
            {
                case 0:return "想在窗边照顾一只小猫。";
                case 4:return "想把一小段旋律练熟。";
                case 6:return "想安静看完一份报纸。";
                default:return "";
            }
        }
        public static int GetWishTopic(int index)
        {
            switch(index) { case 0:return 0;case 4:return 4;case 6:return 3;default:return -1; }
        }
        public static bool HasLifeObject(ResidentState state,int topic,int stage)
        {
            return state!=null&&topic>=0&&topic<5&&state.propsUntil[topic]>0&&state.propsUntil[topic]>=stage;
        }

        // Historical callers must supply the matching stage as well as the event's snapshot.
        // Do not use current-stage object lifetimes to describe an earlier replay frame.
        public static string CurrentLine(ResidentRun run,int index,ResidentState[] snapshot=null,int historicalStage=0)
        {
            if(run==null)return "";
            var states=snapshot??run.residents;
            if(index<0||index>=states.Length)return "";
            var resident=states[index];int wish=GetWishTopic(index),topic=resident.preferredTopic;
            if(topic<0||topic>=5)return "";
            int stage=historicalStage>0?historicalStage:run.Stage;
            if(wish<0)return "今天正在"+Activities[topic]+"。";
            if(topic==wish)
            {
                if(index==0&&topic==0&&HasLifeObject(resident,0,stage)&&resident.lastTopic==0&&resident.reactionKind==ResidentReaction.Overloaded&&resident.interest[0]>=.6f&&resident.PressureRatio(0)>=1)
                    return "划走了猫推送，仍在照顾窗边的小猫。";
                if(resident.interest[wish]>=.5f&&resident.PressureRatio(wish)>=1)
                    return "还在"+Activities[topic]+"；"+ResidentRun.TopicNames[wish]+"推送已经看腻，喜欢的兴趣仍在。";
                return "今天仍在"+Activities[topic]+"。";
            }
            if(HasLifeObject(resident,wish,stage))
                return "今天正被"+ResidentRun.TopicNames[topic]+"吸引；"+ResidentRun.TopicNames[wish]+"用品仍留在房间里。";
            return "今天正被"+ResidentRun.TopicNames[topic]+"吸引，正在"+Activities[topic]+"。";
        }

        public static string TopicPathLine(ResidentRun run,int topic,ResidentState[] snapshot=null,int historicalStage=0)
        {
            if(run==null||topic<0||topic>=5)return "";
            var states=snapshot??run.residents;int stage=historicalStage>0?historicalStage:run.Stage;
            var willing=new List<string>();var overloaded=new List<string>();var oldObjects=new List<string>();
            for(int i=0;i<states.Length;i++)
            {
                var resident=states[i];string room=ResidentRun.RoomOf(i);
                // Continuing willingness, liking and current activity are separate observations.
                if(resident.WillingTopic()==topic)willing.Add(room);
                if(resident.interest[topic]>=.5f&&resident.PressureRatio(topic)>=1)overloaded.Add(room);
                if(resident.preferredTopic!=topic&&HasLifeObject(resident,topic,stage))oldObjects.Add(room);
            }
            string title=ResidentRun.TopicNames[topic];
            return "「"+title+"」仍愿继续接触："+Rooms(willing)+"\n"+
                "仍喜欢，却已看腻推送："+Rooms(overloaded)+"\n"+
                "活动转向别处，旧用品还在："+Rooms(oldObjects);
        }

        // Six ending categories stay unchanged. The different paths inside an ending are
        // rendered from real rooms, personal pressure and recorded neighbour deliveries.
        public static string EndingPathSummary(ResidentRun run,int maxKeyEvents=3)
        {
            if(run==null)return "";
            var scores=run.Scores();int highest=scores.Max();
            var leaders=Enumerable.Range(0,5).Where(topic=>scores[topic]==highest).ToArray();
            var text=new StringBuilder();
            text.AppendLine(leaders.Length>1?"势均力敌的几种生活":"同样的榜首，也会留下不同的生活");
            for(int i=0;i<leaders.Length;i++)
            {
                if(i>0)text.AppendLine();
                text.AppendLine(TopicPathLine(run,leaders[i]));
            }
            var shares=run.events.Where(IsNeighbourDelivery).ToArray();
            int direct=run.events.Count(e=>e.kind=="玩家推荐"&&e.source==-1);
            text.AppendLine();text.AppendLine("玩家直接推荐 "+direct+" 次 · 邻居实际分享 "+shares.Length+" 次");
            if(shares.Length>0)
            {
                var counts=new List<string>();
                for(int topic=0;topic<5;topic++)
                {
                    int count=shares.Count(e=>e.topic==topic);
                    if(count>0)counts.Add(ResidentRun.TopicNames[topic]+" "+count+" 次");
                }
                text.AppendLine(string.Join(" · ",counts.ToArray()));
                foreach(var delivery in shares.Skip(Math.Max(0,shares.Length-Math.Max(0,maxKeyEvents))))
                    text.AppendLine("第 "+delivery.stage+" 阶段 · "+ResidentRun.RoomOf(delivery.source)+" → "+ResidentRun.RoomOf(delivery.resident)+"：「"+ResidentRun.TopicNames[delivery.topic]+"」");
            }
            var changes=run.events.Where(e=>e.resident>=0&&e.previousTopic>=0&&e.previousTopic<5&&
                e.currentTopic>=0&&e.currentTopic<5&&e.previousTopic!=e.currentTopic).ToArray();
            if(changes.Length>0)
            {
                text.AppendLine();text.AppendLine("实际发生的活动转向");
                foreach(var change in changes.Skip(Math.Max(0,changes.Length-Math.Max(0,maxKeyEvents))))
                    text.AppendLine("第 "+change.stage+" 阶段 · "+ResidentRun.RoomOf(change.resident)+"："+
                        ResidentRun.TopicNames[change.previousTopic]+" → "+ResidentRun.TopicNames[change.currentTopic]+
                        "（"+(change.source==-1?"收到你的推荐":change.source>=0?"收到 "+ResidentRun.RoomOf(change.source)+" 的分享":"生活记录")+"）");
            }
            text.AppendLine();text.AppendLine("再看看他们今天的生活");
            foreach(int index in FocusResidents)if(index<run.residents.Length)
                text.AppendLine(FocusLabel(index)+"："+CurrentLine(run,index));
            return text.ToString().TrimEnd();
        }

        static bool IsNeighbourDelivery(RunEvent value)=>value.kind=="邻居分享"&&value.source>=0&&value.resident>=0&&value.topic>=0&&value.topic<5;
        static string Rooms(List<string> rooms)=>rooms.Count==0?"暂无":string.Join("、",rooms.ToArray());
    }
}
