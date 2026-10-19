#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class ScatteredSharingSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/ScatteredSharing";
        readonly List<string> checks=new List<string>();
        FrontendServices service;EndingAchievements originalStore;bool originalMotion;string testKey;
        void Check(bool ok,string message)
        {
            checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/Checks.txt",checks);
            if(!ok)throw new InvalidOperationException(message);
        }
        static ResidentRun StartRun(int seed)
        {
            var run=new ResidentRun(9,90,12,seed);run.SetTarget(0);run.Recommend(0,(run.residents[0].preferredTopic+1)%5);run.StartStage();return run;
        }
        static RunEvent[] Shares(ResidentRun run)=>run.events.Where(e=>e.kind=="邻居分享").ToArray();
        static string Signature(ResidentRun run)=>JsonUtility.ToJson(new EventList { events=run.events.ToArray() });
        [Serializable] sealed class EventList { public RunEvent[] events; }
        void Cleanup()
        {
            if(!service||originalStore==null)return;
            service.settings.motion=originalMotion;service.Apply(false);service.ReplaceAchievementStoreForTest(originalStore);originalStore=null;
            PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();service.RequestNewRun();
        }
        void OnDestroy()=>Cleanup();
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;originalMotion=service.settings.motion;
            testKey="EveryoneRecommends.ShareTest."+Guid.NewGuid().ToString("N");originalStore=service.ReplaceAchievementStoreForTest(new EndingAchievements(testKey));
            try
            {
                int sceneSeed=-1;bool window=true,unique=true,limits=true,relations=true,frozen=true,night=false,day=false;
                var timelines=new List<string>();
                for(int seed=1;seed<=80;seed++)
                {
                    var run=StartRun(seed);var topics=run.opening.Select(r=>r.preferredTopic).ToArray();run.FinishEarly();var shares=Shares(run);
                    window&=shares.All(e=>e.tick>=162&&e.tick<=810);
                    unique&=shares.Zip(shares.Skip(1),(a,b)=>b.tick-a.tick>=25).All(x=>x);
                    limits&=shares.GroupBy(e=>e.source).All(g=>g.Count()==1)&&shares.GroupBy(e=>e.resident).All(g=>g.Count()==1);
                    relations&=shares.All(e=>e.source!=e.resident&&ResidentRun.FloorOf(e.source)==ResidentRun.FloorOf(e.resident));
                    frozen&=shares.All(e=>e.topic==topics[e.source]);night|=shares.Any(e=>e.tick>480);day|=shares.Any(e=>e.tick<450);
                    if(sceneSeed<0&&shares.Length>=3)sceneSeed=seed;
                    timelines.Add(seed+": "+string.Join(" | ",shares.Select(e=>e.tick/10f+"s "+ResidentRun.RoomOf(e.source)+" -> "+ResidentRun.RoomOf(e.resident)+" "+ResidentRun.TopicNames[e.topic])));
                }
                File.WriteAllLines(Folder+"/EightySeedTimelines.txt",timelines);
                Check(window&&unique,"Eighty openings: real share events occur inside the window with separate virtual timestamps and >=2.5-second spacing");
                Check(limits&&relations,"Eighty openings: one outgoing/incoming share maximum, same-floor neighbours and no self-share");
                Check(frozen,"Share topics use each stage opening, preventing immediate forwarding chains");
                Check(day&&night&&sceneSeed>0,"Sharing happens in both daytime and night; a live seed contains multiple genuine shares");
                var steady=StartRun(sceneSeed);var fast=StartRun(sceneSeed);var same=StartRun(sceneSeed);
                steady.Tick(16);Check(Shares(steady).Length==0,"No neighbour share during opening/teaching or before the configured window");
                for(int i=0;i<740;i++)steady.Tick(.1f);fast.FinishEarly();same.Tick(90);
                Check(Signature(steady)==Signature(fast)&&Signature(fast)==Signature(same),"Small ticks, one large tick and fast settlement reproduce identical events/results");
                Check(fast.residents.Count(r=>r.recommended)==1,"Neighbour delivery does not consume any extra player opportunity");
                Check(fast.events.Count(e=>e.kind=="黄昏")==1&&!fast.events.Single(e=>e.kind=="黄昏").text.Contains("分享"),"Dusk is a lighting event and never invents a share");
                var originalThresholds=fast.residents.Select(r=>string.Join(",",r.aversionThresholds)).ToArray();
                fast.NextStage();same.NextStage();fast.FinishEarly();same.Tick(90);
                Check(Signature(fast)==Signature(same)&&fast.Stage==2&&Shares(fast).Any(e=>e.stage==2),"The next stage rebuilds a reproducible schedule and can share again");
                Check(originalThresholds.SequenceEqual(fast.residents.Select(r=>string.Join(",",r.aversionThresholds))),"Per-topic personal thresholds survive the new schedule and stage advance");
                var sequence=Shares(fast).Where(e=>e.stage==1).Select(e=>e.tick).ToArray();
                Check(!sequence.SequenceEqual(Shares(fast).Where(e=>e.stage==2).Select(e=>e.tick)),"Different stages do not repeat one fixed timetable");
                var opening=new ResidentRun(9,90,12,sceneSeed);opening.Tick(90);Check(opening.events.Count==0,"An unstarted world cannot advance the hidden sharing schedule");

                service.settings.motion=true;service.Apply(false);service.RetryRun(9,90,12,sceneSeed,new SimulationParameters());service.LoadScene("Residents");while(service.IsTransitioning)yield return null;
                var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
                view.overlay.buttons[1].onClick.Invoke();view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
                Check(view.delivery.receiptBubbles.Length==9&&view.delivery.receiptBubbles.All(r=>r&&r.GetComponent<ReceiptBubbleGraphic>()),"Nine actual resident speech-balloon backgrounds are installed");
                view.ShowResident(0);view.SelectTopic((view.Run.residents[0].preferredTopic+1)%5);int before=view.Run.events.Count;
                Check(!view.delivery.receiptBubbles.Any(r=>r.gameObject.activeSelf),"Preparing a topic creates no incoming-content balloon");
                view.ConfirmRecommendation();yield return new WaitForSecondsRealtime(.15f);
                Check(view.Run.events.Count==before+1&&view.delivery.receiptBubbles[0].gameObject.activeSelf,"Confirmed direct recommendation still creates exactly one real receipt");
                yield return new WaitForSecondsRealtime(1.1f);view.AdvanceStage();view.SelectFloor(0);
                var preview=StartRun(sceneSeed);preview.FinishEarly();var first=Shares(preview)[0];
                view.TickStage(Mathf.Max(0,first.tick*.1f-view.Run.Elapsed));yield return new WaitForSecondsRealtime(.18f);
                Check(Shares(view.Run).Length==1&&view.delivery.CurrentSource==first.source,"First live neighbour share is generated at its own simulation time, not queued at dusk");
                var bubble=view.delivery.receiptBubbles[first.resident];var icon=view.delivery.receiptIcons[first.resident];
                Check(bubble.gameObject.activeInHierarchy&&icon.sprite==view.topicIcons[first.topic]&&icon.transform.parent==bubble&&view.delivery.receiptLabels[first.resident].text=="邻居分享","Neighbour balloon contains the received topic icon and correct source label");
                Check(bubble.anchoredPosition.x>0&&bubble.anchoredPosition.y>0&&!bubble.GetComponent<ReceiptBubbleGraphic>().raycastTarget&&!icon.raycastTarget,"Balloon is above-right of its resident and never intercepts clicks");
                Check(view.delivery.worldArrow.gameObject.activeSelf&&view.delivery.arrow.gameObject.activeSelf,"Live neighbour share shows matching apartment and diagram arrows");
                yield return Capture("Neighbour_Balloon_And_Arrows");
                view.TogglePause();float time=view.Run.Elapsed;int eventCount=view.Run.events.Count;view.TickStage(60);yield return new WaitForSecondsRealtime(3.6f);
                Check(view.Run.Elapsed==time&&view.Run.events.Count==eventCount&&bubble.gameObject.activeSelf,"Manual pause freezes future share events and preserves the displayed balloon");
                service.settings.motion=false;service.Apply(false);view.TogglePause();yield return null;
                Check(bubble.localScale==Vector3.one,"Reduced motion retains a full-size readable static topic balloon");
                view.ShowResident(first.resident);time=view.Run.Elapsed;view.TickStage(20);
                Check(view.Run.Elapsed==time,"Observation also freezes the new sharing schedule");view.CancelRecommendation();
                view.settingsPanel.SetActive(true);time=view.Run.Elapsed;view.TickStage(20);Check(view.Run.Elapsed==time,"Settings also freeze the new sharing schedule");view.settingsPanel.SetActive(false);
                view.SelectFloor(ResidentRun.FloorOf(first.resident)==3?1:3);int floor=view.SelectedFloor;
                view.TickStage(view.Run.Remaining+1);Check(view.SelectedFloor==floor,"Later offscreen share events never steal the selected floor");
                view.ShowSummary();string state=Signature(view.Run);view.BeginReplay(false);view.ReplayStep(1);view.ReplayStep(-1);
                Check(Signature(view.Run)==state&&!view.delivery.receiptBubbles.Any(r=>r.gameObject.activeSelf),"Read-only replay changes neither shares nor random results and clears live balloons");
                view.ShowSummary();view.Restart(true);Check(view.Run.seed==sceneSeed&&!view.Run.Started&&view.Run.events.Count==0&&!view.delivery.IsBusy,"Same-opening retry clears old schedules and feedback");
                checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines(Folder+"/Checks.txt",checks);
            }
            finally { Cleanup(); }
            Destroy(this);
        }
    }
}
#endif
