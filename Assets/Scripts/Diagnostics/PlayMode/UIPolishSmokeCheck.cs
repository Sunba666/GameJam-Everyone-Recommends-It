#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class UIPolishSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/UIPolish";
        readonly List<string> checks=new List<string>();
        FrontendServices service;
        FrontendServices.Preferences originalSettings;
        EndingAchievements originalAchievements;
        string testKey,storedSettings;
        bool hadSettings;
        void Check(bool ok,string label) { checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Library/ui-polish-smoke.txt",checks); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        void Cleanup()
        {
            if(!service||originalSettings==null)return;
            foreach(var settings in FindObjectsOfType<SettingsView>())settings.Hide();
            service.settings=originalSettings;service.Apply(false);originalSettings=null;
            if(hadSettings)PlayerPrefs.SetString(FrontendServices.PreferenceKey,storedSettings);else PlayerPrefs.DeleteKey(FrontendServices.PreferenceKey);
            if(originalAchievements!=null)service.ReplaceAchievementStoreForTest(originalAchievements);
            PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();service.RequestNewRun();
        }
        void OnDestroy() { Cleanup(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;
            originalSettings=JsonUtility.FromJson<FrontendServices.Preferences>(JsonUtility.ToJson(service.settings));
            hadSettings=PlayerPrefs.HasKey(FrontendServices.PreferenceKey);storedSettings=PlayerPrefs.GetString(FrontendServices.PreferenceKey);
            testKey="EveryoneRecommends.UIPolishTests."+Guid.NewGuid().ToString("N");originalAchievements=service.ReplaceAchievementStoreForTest(new EndingAchievements(testKey));
            try
            {
                service.settings.motion=true;service.Apply(false);
                if(SceneManager.GetActiveScene().name!="Residents") { service.RequestNewRun();service.LoadScene("Residents");while(service.IsTransitioning)yield return null; }
                var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);view.Restart(true);
                view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
                Check(UIFeedbackProfile.Default&&view.topics.All(b=>b.GetComponent<UiButtonFeedback>().profile==UIFeedbackProfile.Default),"Shared editable profile controls the topic buttons and motion");
                Check(view.reactionBubbles.Length==9&&view.residents.All(r=>r.GetComponents<ResidentReactionBubble>().Length==1),"Exactly one reaction paper is installed for each original resident");
                view.SelectFloor(2);yield return new WaitForSecondsRealtime(.7f);view.ShowResident(4);
                var card=view.statusCard;
                Check(card.details.fontSize>=17&&!card.details.resizeTextForBestFit&&card.recent&&card.pauseNote.text.Contains("时间暂停"),"Observation card has readable fixed text, separate history and explicit pause note");
                Check(card.details.preferredHeight<=card.details.rectTransform.rect.height,"Current observation text fits without shrinking to tiny type");
                Check(view.residents.Where(r=>r.gameObject.activeInHierarchy).All(r=>!card.CurrentRect.Overlaps(card.InArea(r.rectTransform))),"Larger observation card avoids all three residents");
                int layoutBefore=card.LayoutPassCount,hudBefore=view.HudRefreshCount;float clock=view.Run.Remaining;
                for(int i=0;i<120;i++) { card.Reposition();view.TickStage(0); }
                Check(card.LayoutPassCount-layoutBefore<=1,"One hundred twenty stationary placement requests avoid repeated grid searches");
                Check(view.HudRefreshCount-hudBefore<=1&&view.Run.Remaining==clock,"Stationary observation avoids repeated HUD updates and keeps the clock frozen");
                yield return Capture("Observation_Readable");
                var button=view.topics[4];var feedback=button.GetComponent<UiButtonFeedback>();var rect=(RectTransform)button.transform;
                Vector2 size=rect.sizeDelta,position=rect.anchoredPosition;var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                feedback.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(.18f);
                Check(feedback.VisualScale>1,"Hover gives a subtle visible response");
                feedback.OnPointerDown(pointer);yield return new WaitForSecondsRealtime(.15f);
                Check(feedback.VisualScale<1&&rect.sizeDelta==size&&rect.anchoredPosition==position&&rect.localScale==Vector3.one,"Press animation changes contents without moving or resizing the button hit area");
                feedback.OnPointerUp(pointer);feedback.OnPointerExit(pointer);service.settings.motion=false;service.Apply(false);yield return null;
                Check(feedback.VisualScale==1,"Reduced-motion setting restores neutral button contents immediately");
                service.settings.motion=true;service.Apply(false);
                int events=view.Run.events.Count;view.SelectTopic(4);
                Check(view.Run.events.Count==events&&!view.Run.residents[4].recommended&&view.selectionLabel.text.Contains("202")&&view.selectionLabel.text.Contains("音乐"),"Preparing a topic clearly identifies the recipient without sending");
                view.floors[1].onClick.Invoke();Check(card.SelectedResident==4&&view.SelectedTopic==4,"Clicking the current floor preserves an unsubmitted recommendation");
                Check(view.ConfirmRecommendation()&&!view.ConfirmRecommendation(),"Polished confirm still sends exactly once");
                yield return new WaitForSecondsRealtime(.3f);
                Check(view.reactionBubbles[4].IsShowing&&view.reactionBubbles[4].RawText==view.Run.residents[4].reaction,"Paper reaction displays the actual model reaction");
                yield return Capture("Recommendation_PaperFeedback");
                view.AdvanceStage();view.TogglePause();yield return new WaitForSecondsRealtime(3.7f);
                Check(view.delivery.receiptIcons[4].gameObject.activeSelf&&view.reactionBubbles[4].IsShowing,"Manual pause preserves receipt and reaction lifetimes instead of silently expiring them");
                view.settingsButton.onClick.Invoke();view.TickStage(0);Check(view.stageProgress.hint.text.Contains("设置中"),"HUD explains that settings are pausing world time");
                var settings=view.settingsPanel.GetComponent<SettingsView>();int saves=service.SettingsSaveCount;
                for(int i=0;i<50;i++)settings.master.value=.25f+i*.005f;
                Check(service.SettingsSaveCount==saves&&Mathf.Abs(AudioListener.volume-settings.master.value)<.001f,"Fifty slider changes apply audio live without fifty save writes");
                settings.Hide();Check(service.SettingsSaveCount==saves+1,"Closing settings flushes the pending values once");
                view.TogglePause();view.SelectFloor(1);service.settings.motion=false;service.Apply(false);yield return null;
                Check(!view.floorDetails.IsTransitioning&&view.floorDetails.CurrentFloor==1&&view.floorDetails.stage.localScale==Vector3.one,"Turning motion off during floor entry finishes safely without leaving a partial zoom");
                service.settings.motion=true;service.Apply(false);view.ShowResident(6);view.TickStage(0);
                Check(view.stageProgress.hint.text.Contains("观察住户")&&view.Run.Remaining>0,"HUD explains observation pause independently of manual pause");
                view.CancelRecommendation();view.Run.Tick(76);view.TickStage(0);
                Check(view.stageProgress.timeLabel.color.r>view.stageProgress.timeLabel.color.g&&view.Run.Remaining<=15,"Final fifteen seconds receive a calm readable warning color");
                view.Run.FinishEarly();view.ShowSummary();view.BeginReplay(false);
                Check(view.reactionBubbles.All(b=>!b.IsShowing)&&!view.delivery.IsBusy,"Read-only replay clears live feedback without resending");
                service.LoadScene("MainMenu");while(service.IsTransitioning)yield return null;
                var menu=FindObjectOfType<MenuView>();
                Check(menu.start.GetComponent<UiButtonFeedback>()&&menu.settings.GetComponent<UiButtonFeedback>()&&menu.achievements.GetComponent<UiButtonFeedback>(),"Title controls share the same feedback component");
                menu.ShowAchievements();yield return new WaitForSecondsRealtime(.7f);
                var achievements=menu.achievementsPanel.GetComponent<AchievementsView>();
                Check(achievements.cards.All(c=>c.button.GetComponent<UiButtonFeedback>()),"All six achievement cards retain clickable feedback");
                yield return Capture("Achievements_ConsistentFeedback");
                menu.ClosePanels();checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/ui-polish-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);
            }
            finally { Cleanup(); }
            Destroy(this);
        }
    }
}
#endif
