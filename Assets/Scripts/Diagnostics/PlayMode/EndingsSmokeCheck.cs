#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class EndingsSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/Endings";
        readonly List<string> checks=new List<string>();
        FrontendServices service;
        EndingAchievements originalStore;
        string temporaryKey,emptyKey;
        bool productionHadKey;
        int productionValue;
        [Serializable] sealed class StrategyOutcome { public int seed,policy;public EndingId ending;public int[] scores; }
        [Serializable] sealed class StrategyReport { public List<StrategyOutcome> runs=new List<StrategyOutcome>(); }
        void Check(bool ok,string name) { checks.Add((ok?"PASS ":"FAIL ")+name);File.WriteAllLines("Library/endings-smoke.txt",checks); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        static void FinishRun(ResidentRun run)
        {
            run.SetTarget(0);run.Recommend(0,0);run.StartStage();
            while(!run.Finished) { run.FinishEarly();if(!run.Finished)run.NextStage(); }
        }
        static void Assign(ResidentRun run,int[] counts)
        {
            int resident=0;
            for(int topic=0;topic<5;topic++)for(int n=0;n<counts[topic];n++)
            {
                var r=run.residents[resident++];r.preferredTopic=topic;
                for(int t=0;t<5;t++) { r.interest[t]=t==topic?.95f:.05f;r.saturation[t]=0; }
            }
        }
        static ResidentRun SampleStrategy(int seed,int policy)
        {
            var run=new ResidentRun(9,90,12,seed);int topic=policy<5?policy:0;
            run.SetTarget(topic);run.Recommend(0,topic);run.StartStage();
            while(!run.Finished)
            {
                // Use normal inputs only: build interest for three stages, then allow recovery.
                if(policy!=5&&run.Stage<=3)for(int i=0;i<9;i++)run.Recommend(i,policy==6?i%5:topic);
                run.FinishEarly();if(!run.Finished)run.NextStage();
            }
            return run;
        }
        void Cleanup()
        {
            if(originalStore!=null&&service) { service.ReplaceAchievementStoreForTest(originalStore);originalStore=null;service.RequestNewRun(); }
            if(!string.IsNullOrEmpty(temporaryKey)) { PlayerPrefs.DeleteKey(temporaryKey);PlayerPrefs.DeleteKey(emptyKey);PlayerPrefs.Save();temporaryKey=null; }
        }
        void OnDestroy() { Cleanup(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;
            productionHadKey=PlayerPrefs.HasKey(EndingAchievements.SaveKey);productionValue=PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1);
            temporaryKey="EveryoneRecommends.EndingTests."+Guid.NewGuid().ToString("N");emptyKey=temporaryKey+".empty";
            var store=new EndingAchievements(temporaryKey);originalStore=service.ReplaceAchievementStoreForTest(store);
            try
            {
                if(SceneManager.GetActiveScene().name!="Residents") { service.RequestNewRun();service.LoadScene("Residents");while(service.IsTransitioning)yield return null; }
                var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
                Check(view.endings&&view.endings.endings.Length==6&&view.endings.endings.Select(e=>e.id).Distinct().Count()==6,"Catalog has six distinct stable ending IDs and editable presentation");
                Check(store.Count==0&&!store.Unlock(EndingId.None),"Fresh progress starts locked and invalid outcomes cannot unlock");
                view.Restart(false);view.Run.SetTarget(0);view.Run.Recommend(0,0);view.Run.StartStage();view.Run.FinishEarly();view.ShowSummary();
                Check(view.Run.Stage==1&&view.Run.Ending()==EndingId.None&&store.Count==0,"An ordinary stage summary never unlocks an ending");
                string heading=view.overlay.heading.text;view.ShowFinal();
                Check(view.overlay.heading.text==heading&&store.Count==0,"Incomplete runs cannot show or award a final ending");
                for(int topic=0;topic<5;topic++)
                {
                    view.Restart(false);FinishRun(view.Run);int[] scores=new int[5];scores[topic]=9;Assign(view.Run,scores);
                    Check(view.Run.Ending()==(EndingId)topic&&view.Run.Scores().SequenceEqual(scores),ResidentRun.TopicNames[topic]+" ending follows actual unique first place");
                    view.ShowSummary();Check(store.Count==topic+1&&store.IsUnlocked((EndingId)topic),ResidentRun.TopicNames[topic]+" achievement saves at final settlement before opening the ending");
                    view.ShowFinal();
                    Check(view.overlay.heading.text.Contains(view.endings.Get((EndingId)topic).title)&&view.overlay.body.text.Contains("本局新解锁成就"),ResidentRun.TopicNames[topic]+" ending renders its own title and new achievement");
                    if(topic==0||topic==4)yield return Capture("Ending_"+(EndingId)topic+"_Fixture");
                    if(topic>0)Check(!view.Run.TargetWon()&&view.overlay.body.text.Contains("目标未达成"),"Actual "+ResidentRun.TopicNames[topic]+" ending remains separate from the selected cat goal");
                    int saved=store.Count;view.ShowFinal();view.BeginReplay(true);view.ReplayStep(1);view.ShowFinal();
                    Check(store.Count==saved&&!store.Unlock((EndingId)topic),ResidentRun.TopicNames[topic]+" replay and repeated final views do not award twice");
                }
                view.Restart(false);FinishRun(view.Run);Assign(view.Run,new[]{3,3,2,1,0});view.ShowFinal();
                Check(view.Run.Ending()==EndingId.Balanced&&!view.Run.TargetWon()&&store.IsUnlocked(EndingId.Balanced)&&store.Count==6,"Two tied leaders produce the balanced ending even when five totals differ");
                yield return Capture("Ending_Balanced_Fixture");
                Assign(view.Run,new[]{4,2,2,1,0});Check(view.Run.Ending()==EndingId.Cat,"A tie below first place does not produce the balanced ending");
                Assign(view.Run,new[]{9,0,0,0,0});foreach(var r in view.Run.residents) { r.interest[4]=.8f;r.saturation[0]=2; }
                Check(view.Run.Ending()==EndingId.Music&&view.Run.Scores()[4]==9,"Ending uses willingness including pressure instead of visible activity alone");
                var reloaded=new EndingAchievements(temporaryKey);Check(reloaded.Count==6&&Enumerable.Range(0,6).All(i=>reloaded.IsUnlocked((EndingId)i)),"All six saved achievements survive a new storage instance");
                view.Restart(true);Check(store.Count==6&&service.CurrentEnding==EndingId.None,"Same-opening retry resets the result and preserves achievements");
                view.Restart(false);Check(store.Count==6&&service.CurrentEnding==EndingId.None,"New random opening also preserves achievements");
                var samples=new StrategyReport();
                for(int seed=301;seed<=316;seed++)for(int policy=0;policy<7;policy++)
                {
                    var run=SampleStrategy(seed,policy);samples.runs.Add(new StrategyOutcome{seed=seed,policy=policy,ending=run.Ending(),scores=run.Scores()});
                }
                File.WriteAllText(Folder+"/StrategySamples.json",JsonUtility.ToJson(samples,true));
                Check(samples.runs.Select(r=>r.ending).Distinct().Count()==6,"Normal recommendation/stop policies observe all six endings across 112 sampled runs without editing resident state");
                service.LoadScene("MainMenu");while(service.IsTransitioning)yield return null;
                var menu=FindObjectOfType<MenuView>();menu.ShowAchievements();yield return new WaitForSecondsRealtime(.7f);
                var panel=menu.achievementsPanel.GetComponent<AchievementsView>();
                Check(menu.achievementsPanel.transform.GetSiblingIndex()==menu.achievementsPanel.transform.parent.childCount-1,"Achievement popup renders above the title-screen quit button and other menu controls");
                Check(panel&&panel.cards.Length==6&&panel.cards.All(c=>c.IsUnlocked)&&panel.progress.text.Contains("6 / 6"),"Returning to the menu displays all six saved achievements");
                panel.cards[5].button.onClick.Invoke();Check(panel.details.text.Contains("最高人数并列")&&panel.cards[5].selected.enabled,"Clicking the balanced achievement explains its actual condition");
                Check(panel.cards.All(c=>new[]{c.title,c.condition,c.status}.All(t=>c.GetComponent<RectTransform>().rect.Contains(t.rectTransform.anchoredPosition+t.rectTransform.sizeDelta*.5f)&&c.GetComponent<RectTransform>().rect.Contains(t.rectTransform.anchoredPosition-t.rectTransform.sizeDelta*.5f))),"All achievement labels fit within their card bounds");
                yield return Capture("Achievements_AllUnlocked_TestStore");
                service.ReplaceAchievementStoreForTest(new EndingAchievements(emptyKey));panel.Refresh();panel.cards[0].button.onClick.Invoke();
                Check(panel.cards.All(c=>!c.IsUnlocked)&&panel.progress.text.Contains("0 / 6")&&panel.details.text.Contains("尚未达成"),"Locked cards show the real condition without unlocking on click");
                yield return Capture("Achievements_Locked_TestStore");
                menu.achievementsPanel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Back").onClick.Invoke();
                Check(!menu.achievementsPanel.activeSelf,"Existing achievement back button still closes the panel");
                Cleanup();
                Check(PlayerPrefs.HasKey(EndingAchievements.SaveKey)==productionHadKey&&PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1)==productionValue,"Verification restores the production store and leaves real achievement progress unchanged");
                checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/endings-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);
            }
            finally { Cleanup(); }
            Destroy(this);
        }
    }
}
#endif
