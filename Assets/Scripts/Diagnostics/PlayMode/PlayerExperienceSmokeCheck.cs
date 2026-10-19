#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    public sealed class PlayerExperienceSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/Playability";
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string message) { checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines("Library/playability-smoke.txt",checks); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        static string Thresholds(ResidentRun run)=>string.Join(";",run.residents.Select(r=>string.Join(",",r.aversionThresholds.Select(t=>t.ToString("R")))));
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);var view=FindObjectOfType<ResidentsView>();var service=FrontendServices.Instance;
            view.SendMessage("OnApplicationFocus",true);
            Check(view.residents.Length==9&&view.Run.residents.Length==9&&view.statusIcons.Length==9&&view.propImages.Length==45,"Nine residents exist in both scene and simulation; no hidden tenth recipient");
            Check(Enumerable.Range(0,9).GroupBy(ResidentRun.FloorOf).All(g=>g.Count()==3)&&view.roomLabels.Select(t=>t.text).SequenceEqual(Enumerable.Range(0,9).Select(ResidentRun.RoomOf)),"Each floor has three correctly labelled rooms");
            Check(view.motions.Length==9&&view.delivery.receiptIcons.Length==9&&!view.buildingContent.Find("Resident_10"),"Tenth actor, motion driver, diagram node and receipt badge removed");
            var run=new ResidentRun(9,90,12,381);var same=new ResidentRun(9,90,12,381);var another=new ResidentRun(9,90,12,382);
            Check(Thresholds(run)==Thresholds(same)&&Thresholds(run)!=Thresholds(another),"Threshold profiles reproduce with the same seed and vary with a new opening");
            Check(run.residents.SelectMany(r=>r.aversionThresholds).Distinct().Count()==45&&run.residents.All(r=>r.aversionThresholds.All(t=>t>=.52f&&t<=.88f)),"All 45 resident/topic thresholds differ and remain inside configured bounds");
            var copy=run.residents[0].Copy();float original=run.residents[0].aversionThresholds[0];copy.aversionThresholds[0]=.25f;
            Check(run.residents[0].aversionThresholds[0]==original,"Replay/opening threshold arrays are independent deep copies");
            run.SetTarget(0);run.Recommend(0,0);run.StartStage();run.FinishEarly();string before=Thresholds(run);run.NextStage();Check(Thresholds(run)==before,"Stage advance preserves every personal threshold");
            var contrast=new ResidentRun(9,90,12,721);contrast.SetTarget(0);contrast.Recommend(8,4);contrast.StartStage();
            foreach(int i in new[]{0,1}) { contrast.residents[i].interest[0]=.85f;contrast.residents[i].preferredTopic=0;contrast.residents[i].saturation[0]=.32f; }
            contrast.residents[0].aversionThresholds[0]=.46f;contrast.residents[1].aversionThresholds[0]=.92f;
            contrast.Recommend(0,0);contrast.Recommend(1,0);
            Check(contrast.residents[0].reactionKind==ResidentReaction.Overloaded&&contrast.residents[1].reactionKind!=ResidentReaction.Overloaded,"Identical exposure crosses the sensitive resident threshold but not the tolerant resident threshold");
            Check(contrast.residents[0].preferredTopic==0&&contrast.residents[0].reaction.Contains("喜欢归喜欢")&&contrast.Reflection().Contains("却划走"),"Liking an activity and rejecting its push coexist; reflection cites a real recorded event");
            var state=new ResidentState();state.interest[0]=state.interest[1]=.9f;state.saturation[0]=state.saturation[1]=.65f;state.aversionThresholds[0]=.4f;state.aversionThresholds[1]=.95f;
            Check(state.WillingTopic()==1,"Final willingness uses personal thresholds, not a global saturation cutoff");
            Check(service.residentsMusic&&service.musicClip!=service.residentsMusic&&service.residentsMusic.channels==2&&Math.Abs(service.residentsMusic.length-65.4545f)<.05,"Distinct 65.45-second stereo gameplay score is imported");
            Check(service.recommendationSounds.Length==5&&service.recommendationSounds.All(c=>c&&c.length>.4f&&c.length<.7f)&&service.recommendationSounds.Distinct().Count()==5,"Five separate short topic earcons are installed");
            yield return new WaitForSecondsRealtime(.8f);
            Check(service.MusicSource.clip==service.residentsMusic&&service.MusicSource.loop&&service.MusicSource.isPlaying,"Opening Residents directly selects and loops the gameplay music");
            int sample=service.MusicSource.timeSamples;yield return new WaitForSecondsRealtime(.2f);
            Check(service.MusicSource.timeSamples!=sample,"Gameplay music advances actual audio playback samples");
            var output=new float[1024];AudioListener.GetOutputData(output,0);
            for(int i=0;i<12&&!output.Any(x=>Mathf.Abs(x)>.00001f);i++) { yield return new WaitForSecondsRealtime(.1f);AudioListener.GetOutputData(output,0); }
            Check(output.Any(x=>Mathf.Abs(x)>.00001f),"Audio mixer renders a non-silent signal in Play mode");
            service.MusicSource.time=service.residentsMusic.length-.1f;yield return new WaitForSecondsRealtime(.35f);
            Check(service.MusicSource.isPlaying&&service.MusicSource.time<1,"Gameplay audio actually wraps across the loop boundary without stopping");
            service.RetryRun(9,90,12,381,view.simulation);service.LoadScene("Residents");while(service.IsTransitioning)yield return null;
            view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
            view.overlay.buttons[1].onClick.Invoke();view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
            int sounds=service.RecommendationSoundsPlayed;view.ShowResident(0);view.SelectTopic(4);yield return null;
            Check(service.RecommendationSoundsPlayed==sounds&&!view.Run.residents[0].recommended,"Preparing content does not play a topic delivery sound or consume the opportunity");
            view.CancelRecommendation();Check(service.RecommendationSoundsPlayed==sounds,"Cancelling does not enqueue a topic sound");
            view.ShowResident(0);view.SelectTopic(4);view.ConfirmRecommendation();yield return new WaitForSecondsRealtime(.12f);
            Check(service.RecommendationSoundsPlayed==sounds+1&&service.LastSoundTopic==4&&view.delivery.receiptIcons[0].sprite==view.topicIcons[4],"Confirmed Music recommendation plays one matching earcon and receipt");
            view.ConfirmRecommendation();Check(service.RecommendationSoundsPlayed==sounds+1,"Repeat confirmation never repeats the topic sound");
            yield return Capture("01_NineResidentsAndDelivery");yield return new WaitForSecondsRealtime(1.05f);view.AdvanceStage();view.ShowResident(0);
            Check(view.statusCard.details.text.Contains("最近两次")&&!view.statusCard.details.text.Contains("阈值")&&!view.statusCard.details.text.Contains("饱和"),"Observation adds a real contact trail without exposing thresholds or saturation");
            yield return Capture("02_ContactTrail");view.CancelRecommendation();
            view.paceButton.onClick.Invoke();Check(view.ObservationSpeed==2&&view.paceLabel.text.Contains("2"),"Observation button selects 2x speed");
            view.paceButton.onClick.Invoke();Check(view.ObservationSpeed==4,"Observation button selects 4x speed");
            view.paceButton.onClick.Invoke();Check(view.ObservationSpeed==1,"Observation speed cycles back to 1x");
            view.TogglePause();float remaining=view.Run.Remaining;view.TickStage(8);Check(view.Run.Remaining==remaining,"Manual pause remains effective with the new observation controls");view.TogglePause();
            // Real user recommendation flow for each topic, with separate audible presentation.
            var heard=new HashSet<int>();
            for(int t=0;t<5;t++) { view.ShowResident(t+1);view.SelectTopic(t);view.ConfirmRecommendation();yield return new WaitForSecondsRealtime(1.06f);heard.Add(service.LastSoundTopic); }
            Check(heard.Count==5,"Actual confirmations trigger all five corresponding audio cues");
            view.ShowResident(0);view.SelectTopic(0);int chance=view.Run.events.Count;view.ConfirmRecommendation();
            Check(view.Run.events.Count==chance,"No extra direct opportunity is introduced by the new feedback controls");view.CancelRecommendation();
            // Present a genuine saved share from a deterministic run, not a fabricated actor reaction.
            ResidentRun spread=null;RunEvent share=null;
            for(int seed=1;seed<30&&share==null;seed++) { spread=new ResidentRun(9,90,12,seed);spread.SetTarget(0);spread.Recommend(0,0);spread.StartStage();spread.Tick(45);share=spread.events.FirstOrDefault(e=>e.kind=="邻居分享"); }
            Check(share!=null&&spread.events.Where(e=>e.kind=="邻居分享").GroupBy(e=>e.source).All(g=>g.Count()==1)&&spread.events.Where(e=>e.kind=="邻居分享").GroupBy(e=>e.resident).All(g=>g.Count()==1),"Dusk still respects one outgoing/incoming share per resident");
            view.delivery.Clear();view.SelectFloor(0);view.TickStage(Mathf.Max(0,45-view.Run.Elapsed));
            share=view.Run.events.FirstOrDefault(e=>e.kind=="邻居分享");
            Check(share!=null,"Actual scene dusk generates a recorded neighbour share");
            yield return new WaitForSecondsRealtime(.24f);
            Check(view.delivery.CurrentSource==share.source&&view.delivery.arrow.gameObject.activeSelf&&view.delivery.worldArrow.gameObject.activeSelf&&view.delivery.routeLabel.text.Contains(ResidentRun.RoomOf(share.source))&&view.delivery.routeLabel.text.Contains(ResidentRun.RoomOf(share.resident)),"Neighbour share presents correct source/recipient arrows in both diagram and apartment");
            Check(view.delivery.receiptLabels[share.resident].text=="邻居分享"&&service.LastSoundTopic==share.topic,"Neighbour receipt and audio use the actual shared topic/source");yield return Capture("03_NeighbourShare");
            Check(view.delivery.IsBusy,"Dusk presentation remains queued while the recorded simulation is already settled");
            view.TickStage(view.Run.Remaining+.2f);
            Check(view.Run.StageEnded&&view.Run.Remaining==0&&!view.overlay.gameObject.activeSelf,"Finishing the stage keeps live share arrows visible before showing the summary");
            int settledEvents=view.Run.events.Count;float waited=0;
            while(!view.overlay.gameObject.activeSelf&&waited<12) { view.SendMessage("OnApplicationFocus",true);yield return new WaitForSecondsRealtime(.2f);waited+=.2f; }
            Check(view.overlay.gameObject.activeSelf&&view.Run.events.Count==settledEvents,"Summary opens after presentation; showing effects does not rerun simulation");
            int eventCount=view.Run.events.Count;sounds=service.RecommendationSoundsPlayed;
            view.BeginReplay(false);view.ReplayStep(1);yield return new WaitForSecondsRealtime(.2f);
            Check(eventCount==view.Run.events.Count&&sounds==service.RecommendationSoundsPlayed&&!view.delivery.worldArrow.gameObject.activeSelf,"Replay remains read-only and does not play live recommendation sounds");
            view.ShowSummary();yield return Capture("04_ReflectionSummary");
            view.Restart(true);Check(view.ObservationSpeed==1&&!view.delivery.IsBusy,"Retry clears transient presentation and restores normal observation speed");
            service.LoadScene("MainMenu");while(service.IsTransitioning)yield return null;yield return new WaitForSecondsRealtime(.85f);
            Check(service.MusicSource.clip==service.musicClip&&service.MusicSource.isPlaying,"Returning to the title crossfades to the original menu music");
            service.RequestNewRun();service.LoadScene("Residents");while(service.IsTransitioning)yield return null;yield return new WaitForSecondsRealtime(.85f);
            view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
            Check(view.residents.Length==9&&service.MusicSource.clip==service.residentsMusic&&service.MusicSource.isPlaying,"A new game crossfades back to gameplay music and retains nine residents");
            checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/playability-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);Destroy(this);
        }
    }
}
#endif
