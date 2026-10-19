using System;

namespace EveryoneRecommends.Frontend
{
    // One private attempt per resident, scheduled on the simulation's 0.1-second clock.
    // A separate random stream keeps opening generation and player reaction rolls intact.
    public sealed class ResidentShareSchedule
    {
        public struct Attempt
        {
            public int tick, sender;
            public double acceptanceRoll, recipientRoll;
        }
        readonly Attempt[] attempts;
        int next;
        public ResidentShareSchedule(int seed,int stage,int count,float duration,SimulationParameters rules)
        {
            var random=new Random(unchecked(seed*397 ^ stage*7919 ^ 0x531A9));
            int lastTick=Math.Max(1,(int)Math.Ceiling(duration*10));
            int start=Math.Max(1,Math.Min(lastTick,(int)Math.Ceiling(duration*Clamp(rules.shareWindowStart,0,1)*10)));
            int end=Math.Max(start,Math.Min(lastTick,(int)Math.Floor(duration*Clamp(rules.shareWindowEnd,0,1)*10)));
            // Fit even a shortened debug stage without an unbounded retry loop or tied attempts.
            int population=count;count=Math.Min(count,end-start+1);
            int gap=count<=1?0:Math.Max(1,Math.Min((int)Math.Ceiling(Math.Max(.1f,rules.shareMinimumGap)*10),(end-start)/(count-1)));
            int spare=end-start-gap*Math.Max(0,count-1);
            var offsets=new int[count];var order=new int[population];
            for(int i=0;i<count;i++)offsets[i]=random.Next(spare+1);
            for(int i=0;i<population;i++)order[i]=i;
            Array.Sort(offsets);
            for(int i=population-1;i>0;i--) { int other=random.Next(i+1),swap=order[i];order[i]=order[other];order[other]=swap; }
            attempts=new Attempt[count];
            for(int i=0;i<count;i++)attempts[i]=new Attempt { tick=start+offsets[i]+gap*i,sender=order[i],acceptanceRoll=random.NextDouble(),recipientRoll=random.NextDouble() };
        }
        public bool TryTake(int tick,out Attempt attempt)
        {
            if(next<attempts.Length&&attempts[next].tick<=tick) { attempt=attempts[next++];return true; }
            attempt=default;return false;
        }
        static float Clamp(float value,float min,float max)=>Math.Max(min,Math.Min(max,value));
    }
}
