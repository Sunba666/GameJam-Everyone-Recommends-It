#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // Exercises the real scene UI with explicit virtual time, isolated achievement storage.
    public sealed class ReleasePolishSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/ReleasePolish";
        readonly List<string> checks=new List<string>();
        FrontendServices service;
        EndingAchievements originalStore;
        string testKey;
        ResidentsView view;
        bool originalMotion;
        void Check(bool ok,string text)
        {
            checks.Add((ok?"PASS ":"FAIL ")+text);
            File.WriteAllLines(Folder+"/Checks.txt",checks);
        }
        static string Signature(ResidentRun run)=>run.Stage+"|"+run.Elapsed+"|"+run.Available+"|"+
            string.Join("\n",run.residents.Select(JsonUtility.ToJson))+"\n"+string.Join("\n",run.events.Select(JsonUtility.ToJson));
        bool NoHighlight()=>view.residents.All(r=>!r.GetComponent<UnityEngine.UI.Outline>()||!r.GetComponent<UnityEngine.UI.Outline>().enabled);
        void Cleanup()
        {
            if(!service||originalStore==null)return;
            service.ReplaceAchievementStoreForTest(originalStore);originalStore=null;
            service.settings.motion=originalMotion;service.Apply(false);
            PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();
            if(view){view.enabled=true;view.Restart(false);}
        }
        void OnDestroy()=>Cleanup();
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;
            string savedSettings=PlayerPrefs.GetString(FrontendServices.PreferenceKey,"");
            bool hadAchievement=PlayerPrefs.HasKey(EndingAchievements.SaveKey);
            int achievement=PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1);
            originalMotion=service.settings.motion;testKey="EveryoneRecommends.ReleaseTest."+Guid.NewGuid().ToString("N");
            originalStore=service.ReplaceAchievementStoreForTest(new EndingAchievements(testKey));
            try
            {
                service.settings.motion=false;service.Apply(false);
                view=FindObjectOfType<ResidentsView>();
                if(!view)throw new InvalidOperationException("Open Residents before running releaseqa");
                view.Restart(false);view.enabled=false;view.SendMessage("OnApplicationFocus",true);
                int seed=view.Run.seed;File.WriteAllText(Folder+"/Seed.txt",seed.ToString());
                view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
                Check(view.Run.Target==0&&!view.Run.Started,"Goal selection is separate from tutorial and clock");
                view.ShowResident(0);string before=Signature(view.Run);view.SelectTopic(0);
                Check(before==Signature(view.Run),"Selecting a topic changes no state or opportunity");
                Check(view.ConfirmRecommendation()&&!view.ConfirmRecommendation(),"Tutorial confirms exactly once");
                view.stageProgress.next.onClick.Invoke();
                Check(view.Run.Started&&view.Run.residents[0].recommended,"Start keeps the tutorial's consumed opportunity");
                for(int stage=1;stage<=12;stage++)
                {
                    int resident=stage%9;
                    view.ShowResident(resident);view.SelectTopic(stage%5);
                    Check(view.ConfirmRecommendation(),"Stage "+stage+": a real resident card submits a recommendation");
                    view.delivery.Clear();
                    float clock=view.Run.Elapsed;view.ShowResident((resident+1)%9);view.TickStage(2);
                    Check(view.Run.Elapsed==clock,"Stage "+stage+": observation freezes virtual time");
                    view.CancelRecommendation();view.TickStage(25);
                    view.fastForward.onClick.Invoke();view.overlay.buttons[0].onClick.Invoke();view.ShowSummary();
                    Check(view.Run.StageEnded&&view.Run.Stage==stage,"Stage "+stage+": fast-forward confirmation reaches its summary");
                    before=Signature(view.Run);view.BeginReplay(false);
                    Check(!view.overlay.buttons[0].interactable,"Stage "+stage+": first replay event disables Previous");
                    view.ReplayStep(100000);
                    Check(!view.overlay.buttons[2].interactable,"Stage "+stage+": final replay event disables Next");
                    Check(NoHighlight(),"Stage "+stage+": a stage-end event clears stale resident highlighting");
                    Check(before==Signature(view.Run),"Stage "+stage+": replay is read-only");
                    view.overlay.buttons[3].onClick.Invoke();
                    view.overlay.buttons[2].onClick.Invoke();
                    if(stage<12)Check(view.Run.Stage==stage+1&&view.Run.Available==9,"Next stage renews nine opportunities");
                }
                Check(view.Run.Finished&&view.overlay.heading.text.StartsWith("结局"),"Twelve real UI stage transitions reach the final result");
                view.BeginReplay(true);view.ReplayStep(1);view.overlay.buttons[1].onClick.Invoke();view.ReplayStep(-1);
                Check(view.overlay.labels[1].text=="播放回放","Manual replay navigation pauses autoplay");
                view.ReplayStep(100000);before=Signature(view.Run);view.overlay.buttons[1].onClick.Invoke();
                Check(view.overlay.heading.text.StartsWith("回放 1 /")&&view.overlay.labels[1].text=="暂停回放"&&before==Signature(view.Run),
                    "Replay at the end restarts from the first saved event without changing the run");
                view.ReplayStep(-100000);view.overlay.buttons[3].onClick.Invoke();
                Check(NoHighlight(),"Returning from a resident event to final result removes historical highlight");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/FinalResult.png");yield return null;
                view.Restart(true);
                Check(view.Run.seed==seed&&!view.Run.Started&&view.Run.events.Count==0&&view.Run.Available==9,"Same-opening retry clears clock, history and consumed opportunities");
                Check(PlayerPrefs.GetString(FrontendServices.PreferenceKey,"")==savedSettings&&
                    PlayerPrefs.HasKey(EndingAchievements.SaveKey)==hadAchievement&&PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1)==achievement,
                    "Production settings and achievements remain unchanged");
            }
            finally { Cleanup(); }
            service.LoadScene("MainMenu");while(service.IsTransitioning)yield return null;
            var menu=FindObjectOfType<MenuView>();menu.ShowHowTo();
            for(int page=0;page<3;page++)
            {
                menu.SetPage(page);Canvas.ForceUpdateCanvases();
                Check(menu.pageBody.preferredHeight<=menu.pageBody.rectTransform.rect.height+1,"Tutorial page "+(page+1)+": full text fits its reading area");
                yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Folder+"/Tutorial_"+(page+1)+".png");yield return null;
            }
            menu.ClosePanels();
            checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines(Folder+"/Checks.txt",checks);
            Destroy(this);
        }
    }
}
#endif
