#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // Play-mode verification. Natural action paths and explicit copied-state boundary
    // fixtures are reported separately; neither writes the production achievement store.
    public sealed class CreativeBeatsSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/CreativeBeats";
        readonly List<string> checks=new List<string>();
        readonly List<string> paths=new List<string>();
        FrontendServices service;
        EndingAchievements originalStore;
        FrontendServices.Preferences originalPreferences;
        ResidentsView live;
        string testKey,productionSettings;
        bool productionHadAchievement;
        int productionAchievement;

        [Serializable] sealed class RunRecord
        {
            public ResidentState[] residents;
            public RunEvent[] events;
            public int stage,target,available;
            public float elapsed;
            public bool started,ended,finished,tutorial;
        }
        static string Signature(ResidentRun run)=>JsonUtility.ToJson(new RunRecord {
            residents=run.Snapshot(),events=run.events.ToArray(),stage=run.Stage,target=run.Target,available=run.Available,
            elapsed=run.Elapsed,started=run.Started,ended=run.StageEnded,finished=run.Finished,tutorial=run.TutorialDone });
        void Check(bool ok,string message)
        {
            checks.Add((ok?"PASS ":"FAIL ")+message);
            File.WriteAllLines(Folder+"/Checks.txt",checks);File.WriteAllLines("Library/creative-beats-smoke.txt",checks);
            if(!ok)throw new InvalidOperationException(message);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame();
        }
        void Cleanup()
        {
            if(!service||originalStore==null)return;
            if(live)live.enabled=true;
            service.settings=originalPreferences;service.Apply(false);
            service.ReplaceAchievementStoreForTest(originalStore);originalStore=null;
            PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();service.RequestNewRun();
            if(live)live.Restart(false);
        }
        void OnDestroy()=>Cleanup();
        IEnumerator BindCurrentRun()
        {
            service.LoadScene("Residents");while(service.IsTransitioning)yield return null;
            live=FindObjectOfType<ResidentsView>();Check(live&&live.Run==service.CurrentRun,"Scene binds the prepared normal-action run through FrontendServices.CurrentRun");
            // Keep virtual time explicit while the independent presentation components
            // keep updating. This isolates presentation writes from ordinary world ticks.
            live.enabled=false;live.SendMessage("OnApplicationFocus",true);
            if(live.spreadStory)live.spreadStory.SendMessage("OnApplicationFocus",true);
        }
        static bool PrepareCatPath(ResidentRun run,out RunEvent cue)
        {
            cue=null;if(run.opening[0].preferredTopic!=0)return false;
            run.SetTarget(0);run.Recommend(0,0);run.StartStage();
            while(true)
            {
                if(!run.residents[0].recommended)run.Recommend(0,0);
                var last=run.events.LastOrDefault(e=>e.resident==0);
                if(CatCareBeatView.Matches(last,0)&&ResidentLifeStories.HasLifeObject(last.after[0],0,last.stage))
                { cue=last;return true; }
                run.FinishEarly();if(run.Finished)return false;run.NextStage();
            }
        }
        static void PrepareNoPush(ResidentRun run,int stage,float seconds)
        {
            run.SetTarget(0);run.Recommend(0,(run.residents[0].preferredTopic+1)%5);run.StartStage();
            while(run.Stage<stage) { run.FinishEarly();run.NextStage(); }
            run.Tick(seconds);
        }
        static ResidentRun FinishedNormalRun(int seed,int topic,bool observeOnly=false)
        {
            var run=new ResidentRun(9,90,12,seed);run.SetTarget(topic);run.Recommend(0,topic);run.StartStage();
            while(!run.Finished)
            {
                if(!observeOnly&&run.Stage<=3)for(int i=0;i<9;i++)run.Recommend(i,topic);
                run.FinishEarly();if(!run.Finished)run.NextStage();
            }
            return run;
        }
        void VerifyLifeAndEndings(ResidentRun catRun,RunEvent cue)
        {
            Check(ResidentLifeStories.FocusResidents.SequenceEqual(new[]{0,4,6})&&
                ResidentLifeStories.FocusResidents.All(i=>ResidentLifeStories.GetWish(i)!=""),"Exactly three focus residents have stable names and daily wishes");
            var openings=Enumerable.Range(1,80).Select(seed=>new ResidentRun(9,90,12,seed)).ToArray();
            Check(ResidentLifeStories.FocusResidents.All(i=>openings.Select(r=>r.residents[i].preferredTopic).Distinct().Count()==5),"Wishes do not force initial preferences; all three focus residents cover all five topics across 80 natural openings");
            string before=Signature(catRun);
            for(int repeat=0;repeat<4;repeat++)
            {
                foreach(int index in ResidentLifeStories.FocusResidents)ResidentLifeStories.CurrentLine(catRun,index);
                for(int topic=0;topic<5;topic++)ResidentLifeStories.TopicPathLine(catRun,topic);
                ResidentLifeStories.EndingPathSummary(catRun);
            }
            Check(Signature(catRun)==before,"Reading wishes and path summaries changes no resident field, opportunity, time or saved event");
            Check(ResidentLifeStories.CurrentLine(catRun,0,cue.after,cue.stage).Contains("划走")||ResidentLifeStories.CurrentLine(catRun,0,cue.after,cue.stage).Contains("看腻"),"A real cat overload event can describe liking while refusing more content");
            // Boundary fixture: a detached Copy snapshot, never the live run or a
            // claimed natural game outcome, checks an old object's expiry precisely.
            var copied=catRun.Snapshot();copied[0].preferredTopic=4;int expires=copied[0].propsUntil[0];
            string retained=ResidentLifeStories.CurrentLine(catRun,0,copied,expires);
            string expired=ResidentLifeStories.CurrentLine(catRun,0,copied,expires+1);
            Check(retained.Contains("猫用品")&&!expired.Contains("猫用品")&&Signature(catRun)==before,"Detached snapshot fixture: old cat objects remain through their final stage and expire afterwards without writing the run");
            Check(ResidentLifeStories.CurrentLine(catRun,0,cue.after,cue.stage)==ResidentLifeStories.CurrentLine(catRun,0,cue.after,cue.stage),"An earlier event snapshot and matching historical stage always produce the same life line");

            var endings=new Dictionary<EndingId,ResidentRun>();ResidentRun firstCat=null,anotherCat=null;
            for(int seed=301;seed<=380&&(endings.Count<6||anotherCat==null);seed++)
            for(int policy=0;policy<6;policy++)
            {
                var run=FinishedNormalRun(seed,policy==5?0:policy,policy==5);var ending=run.Ending();
                if(!endings.ContainsKey(ending)) { endings.Add(ending,run);paths.Add("Natural ending "+ending+": seed="+seed+", policy="+(policy==5?"tutorial then stop":"topic "+policy+" to all during stages 1..3 then stop")); }
                if(ending==EndingId.Cat)
                {
                    if(firstCat==null)firstCat=run;
                    else if(ResidentLifeStories.TopicPathLine(firstCat,0)!=ResidentLifeStories.TopicPathLine(run,0))anotherCat=run;
                }
            }
            Check(endings.Count==6,"All six ending categories are observed from normal recommendations and stop policies, without forced resident scores");
            foreach(var pair in endings)
            {
                string state=Signature(pair.Value),text=ResidentLifeStories.EndingPathSummary(pair.Value);
                var scores=pair.Value.Scores();int top=scores.Max();
                Check(Enumerable.Range(0,5).All(t=>text.Contains("「"+ResidentRun.TopicNames[t]+"」仍愿继续接触：")== (scores[t]==top)),"Actual leading-topic room sections match the "+pair.Key+" ending, including ties");
                Check(state==Signature(pair.Value)&&pair.Value.Ending()==pair.Key,"Reading the "+pair.Key+" path does not reroll or mutate its final result");
                File.WriteAllText(Folder+"/Path_"+pair.Key+".txt",text);
            }
            Check(firstCat!=null&&anotherCat!=null,"Two natural cat-first endings have different willingness, overload or remaining-object room lists");
            File.WriteAllText(Folder+"/SameWinner_DifferentLives.txt","First seed="+firstCat.seed+"\n"+ResidentLifeStories.EndingPathSummary(firstCat)+"\n\nSecond seed="+anotherCat.seed+"\n"+ResidentLifeStories.EndingPathSummary(anotherCat));
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;
            Check(service,"Frontend service is available");
            originalPreferences=JsonUtility.FromJson<FrontendServices.Preferences>(JsonUtility.ToJson(service.settings));
            productionSettings=PlayerPrefs.GetString(FrontendServices.PreferenceKey,"");
            productionHadAchievement=PlayerPrefs.HasKey(EndingAchievements.SaveKey);productionAchievement=PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1);
            testKey="EveryoneRecommends.CreativeBeatsTest."+Guid.NewGuid().ToString("N");
            originalStore=service.ReplaceAchievementStoreForTest(new EndingAchievements(testKey));
            try
            {
                service.settings.motion=true;service.Apply(false);
                int catSeed=-1;RunEvent cue=null;ResidentRun catSample=null;
                for(int seed=1;seed<=80;seed++)
                {
                    var sample=new ResidentRun(9,90,12,seed);
                    if(!PrepareCatPath(sample,out var found))continue;catSeed=seed;catSample=sample;cue=found;break;
                }
                Check(catSeed>0&&cue!=null,"An opening among seeds 1..80 naturally reaches cat overload while preserving cat interest and a real life object");
                paths.Add("Cat path: seed="+catSeed+", initial 301 topic=cat; target=cat; tutorial 301 cat at stage 1 tick 0; stage 2.."+cue.stage+" recommend 301 cat at tick 0; finish each prior stage normally. Cue stage="+cue.stage+", tick="+cue.tick+", interest="+cue.after[0].interest[0]+", pressure="+cue.after[0].saturation[0]+", threshold="+cue.after[0].aversionThresholds[0]+", cat propsUntil="+cue.after[0].propsUntil[0]);
                File.WriteAllLines(Folder+"/NaturalActionPaths.txt",paths);
                VerifyLifeAndEndings(catSample,cue);
                var boundCat=service.RetryRun(9,90,12,catSeed,new SimulationParameters());RunEvent liveCue;
                Check(PrepareCatPath(boundCat,out liveCue)&&Signature(boundCat)==Signature(catSample),"Replaying the recorded cat action path reproduces identical actual states and events");
                yield return BindCurrentRun();
                Check(live.catCare&&live.spreadStory&&live.statusCard.cluePanel&&live.statusCard.informationToggle&&live.statusCard.life,"Cat care, saved-spread story and both card information modes are installed");
                live.SelectFloor(3);while(live.floorDetails&&live.floorDetails.IsTransitioning)yield return null;
                live.TickStage(0);if(live.delivery)live.delivery.Clear();
                string catState=Signature(live.Run);yield return new WaitForSecondsRealtime(.12f);
                Check(live.catCare.CareVisible&&live.catCare.IsPlaying&&live.catCare.body.sprite==live.catCare.tabletPose&&live.catCare.swipedCard.gameObject.activeSelf,"Real recorded overload starts the visible video-swipe pose");
                yield return Capture("Cat_Swipe_NaturalActionPath");yield return new WaitForSecondsRealtime(1.25f);
                Check(live.catCare.body.sprite==live.catCare.carePose&&live.catCare.cat.gameObject.activeSelf&&!live.catCare.swipedCard.gameObject.activeSelf,"The same resident turns from swiping the video to caring for the real cat");
                yield return Capture("Cat_Care_NaturalActionPath");
                Check(Signature(live.Run)==catState,"Cat poses and visual callbacks do not change the prepared formal state or create new events");
                live.TogglePause();float beat=live.catCare.BeatTime;yield return new WaitForSecondsRealtime(.2f);
                Check(live.catCare.BeatTime==beat,"Manual pause freezes cat-beat playback time");live.TogglePause();
                service.settings.motion=false;service.Apply(false);yield return null;
                Check(live.catCare.body.sprite==live.catCare.carePose&&live.catCare.body.rectTransform.localEulerAngles==Vector3.zero,"Reduced motion retains the meaningful cat-care pose without wobble");

                live.ShowResident(0);var card=live.statusCard;card.Refresh();string cardState=Signature(live.Run);
                Check(Enumerable.Range(0,5).All(t=>Mathf.Abs(card.cells.Skip(t*10).Take(10).Sum(c=>c.fillAmount)/10-live.Run.residents[0].saturation[t])<.001f),"Complete status bars read the same formal pressures as before");
                yield return Capture("Status_Complete_SameRun");card.informationToggle.onClick.Invoke();
                Check(card.ClueMode&&card.cluePanel.activeSelf&&card.clueLines[0].text=="推送划走了","Behaviour-clue mode replaces exact display with an observation from the existing overload event");
                Check(card.clueLines.All(t=>t.text!="")&&Signature(live.Run)==cardState,"Information switching reads saved evidence without advancing time, recomputing probabilities or adding events");
                yield return Capture("Status_BehaviourClues_SameRun");
                card.informationToggle.onClick.Invoke();Check(!card.ClueMode&&!card.cluePanel.activeSelf&&Signature(live.Run)==cardState,"Returning to complete information restores the same data");
                card.life.onClick.Invoke();Check(live.overlay.body.text.Contains(ResidentLifeStories.GetWish(0))&&live.overlay.body.text.Contains(ResidentLifeStories.CurrentLine(live.Run,0)),"The focus resident's life panel combines a daily wish with its true current outcome");
                Check(Signature(live.Run)==cardState,"Opening the resident life panel changes no formal state");live.overlay.buttons[0].onClick.Invoke();live.CancelRecommendation();

                int storySeed=-1;
                for(int seed=1;seed<=80;seed++)
                {
                    var sample=new ResidentRun(9,90,12,seed);PrepareNoPush(sample,3,80);
                    if(!sample.events.Any(e=>e.stage==3&&e.kind=="邻居分享"&&e.previousTopic>=0&&e.currentTopic!=e.previousTopic))continue;
                    if(!sample.events.Any(e=>e.stage==1&&e.kind=="邻居分享"))continue;
                    storySeed=seed;break;
                }
                Check(storySeed>0,"A third-stage activity-changing neighbour share occurs naturally after teaching with no more player recommendations");
                var storyRun=service.RetryRun(9,90,12,storySeed,new SimulationParameters());PrepareNoPush(storyRun,3,80);
                paths.Add("Spread path: seed="+storySeed+", target=cat; tutorial 301 topic=(opening topic+1)%5 at stage 1 tick 0; no further direct recommendation; finish stages 1 and 2; advance stage 3 to 80 seconds. A mid-stage debug entry isolates the story's recommendation lock while normal opportunities remain.");
                File.WriteAllLines(Folder+"/NaturalActionPaths.txt",paths);
                yield return BindCurrentRun();service.settings.motion=false;service.Apply(false);live.SelectFloor(1);
                while(live.floorDetails&&live.floorDetails.IsTransitioning)yield return null;
                live.TickStage(0);if(live.delivery)live.delivery.Clear();live.ShowResident(6);live.SelectTopic(0);
                Check(live.CanConfirm&&!storyRun.StageEnded,"Before story playback the ongoing natural run has a valid unused recommendation opportunity");
                string prepared=Signature(storyRun);live.statusCard.life.onClick.Invoke();live.overlay.buttons[0].onClick.Invoke();
                Check(live.SelectedTopic==0&&live.CanConfirm&&Signature(storyRun)==prepared,"Reading a daily wish preserves a prepared recommendation without sending or consuming it");live.CancelRecommendation();
                int originalFloor=live.SelectedFloor;string storyState=Signature(storyRun);string lighting=live.BackgroundTimeOfDay;
                live.BeginSpreadStory();var story=live.spreadStory;
                Check(story.IsActive&&story.HasShare(3)&&Mathf.Abs(story.duration-12)<.001f&&live.TimerPaused,"A real share starts a twelve-second saved-event story and freezes the world");
                Check(live.topics.All(b=>!b.gameObject.activeSelf)&&live.statusCard.coveredSidebar.All(g=>!g.activeSelf),"Story presentation hides covered controls instead of leaving cropped buttons behind its paper");
                live.ShowResident(6);live.SelectTopic(0);Check(!live.ConfirmRecommendation()&&!live.statusCard.gameObject.activeSelf&&Signature(storyRun)==storyState,"Player selection and confirmation entry points cannot send during story playback");
                float arrowMoment=(story.duration-3)/story.SelectedEvents.Count*.4f;
                yield return new WaitForSecondsRealtime(Mathf.Max(0,arrowMoment-story.PlaybackElapsed));story.pause.onClick.Invoke();
                float paused=story.PlaybackElapsed;yield return new WaitForSecondsRealtime(.2f);
                Check(story.PlaybackPaused&&story.PlaybackElapsed==paused&&story.arrow.gameObject.activeSelf,"Story pause freezes its own clock while showing the actual source-to-receiver arrow");
                yield return Capture("Spread_Actual_Source_Receiver");
                Check(story.CurrentEvent!=null&&story.SelectedEvents.All(e=>storyRun.events.Contains(e)),"The story uses original saved event objects with actual source, receiver and activity data");
                story.pause.onClick.Invoke();story.skip.onClick.Invoke();
                Check(story.DirectRecommendationCount==0&&story.NeighbourShareCount==storyRun.events.Count(e=>e.stage==3&&e.kind=="邻居分享")&&story.body.text.Contains("根本没有再推"),"The no-push conclusion counts actual third-stage recommendations and neighbour shares");
                Check(Enumerable.Range(0,9).All(i=>live.statusIcons[i].sprite==live.topicIcons[story.CurrentEvent.after[i].preferredTopic]),"Story conclusion shows saved activities from the received event snapshot");
                yield return Capture("Spread_NoPlayerPush_Conclusion");story.back.onClick.Invoke();
                Check(!story.IsActive&&live.SelectedFloor==originalFloor&&live.BackgroundTimeOfDay==lighting&&Signature(storyRun)==storyState,"Returning restores floor, current lighting and untouched formal results");
                Check(live.topics.All(b=>b.gameObject.activeSelf)&&live.statusCard.coveredSidebar.All(g=>g.activeSelf),"Returning restores the complete recommendation controls and diagram");
                Check(Enumerable.Range(0,9).All(i=>live.statusIcons[i].sprite==live.topicIcons[storyRun.residents[i].preferredTopic]),"Returning restores every resident's formal current activity");
                Check(story.Begin(1,live.ShowSummary),"The tutorial-stage real share is available for retrospective story playback");story.skip.onClick.Invoke();
                Check(story.DirectRecommendationCount==1&&story.body.text.Contains("教学推荐"),"Tutorial delivery is counted as a real stage-one direct recommendation");story.Stop(true);
                live.BeginSpreadStory();yield return new WaitForSecondsRealtime(story.duration+.15f);
                Check(!story.IsActive&&live.overlay.gameObject.activeSelf&&live.SelectedFloor==originalFloor&&Signature(storyRun)==storyState,"Automatic twelve-second completion returns to summary without changing the run");

                while(!storyRun.Finished) { storyRun.FinishEarly();if(!storyRun.Finished)storyRun.NextStage(); }
                live.TickStage(0);live.ShowSummary();live.ShowFinal();string endingState=Signature(storyRun);
                Check(live.overlay.body.text.Contains(ResidentLifeStories.EndingPathSummary(storyRun)),"The live final screen includes the actual resident paths behind its existing ending category");
                live.ShowFinal();Check(Signature(storyRun)==endingState,"Opening the enriched final screen twice creates no additional result or event");
                yield return Capture("Ending_Actual_ResidentPaths");
                File.WriteAllLines(Folder+"/NaturalActionPaths.txt",paths);
                Cleanup();
                Check(PlayerPrefs.HasKey(EndingAchievements.SaveKey)==productionHadAchievement&&PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1)==productionAchievement,"Production achievement progress survives all natural-run and presentation checks unchanged");
                Check(PlayerPrefs.GetString(FrontendServices.PreferenceKey,"")==productionSettings&&JsonUtility.ToJson(service.settings)==JsonUtility.ToJson(originalPreferences),"Verification restores settings without modifying the production preference record");
                checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines(Folder+"/Checks.txt",checks);File.WriteAllLines("Library/creative-beats-smoke.txt",checks);
            }
            finally { Cleanup(); }
            Destroy(this);
        }
    }
}
#endif
