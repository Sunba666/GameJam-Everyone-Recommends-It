#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class IllustratedStatusCardSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/IllustratedStatus";
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string label) { checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Library/status-card-smoke.txt",checks); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);var service=FrontendServices.Instance;
            string key="EveryoneRecommends.StatusCardTests."+Guid.NewGuid().ToString("N");
            var original=service.ReplaceAchievementStoreForTest(new EndingAchievements(key));bool motion=service.settings.motion;
            try
            {
                service.settings.motion=false;service.Apply(false);
                if(SceneManager.GetActiveScene().name!="Residents") { service.RequestNewRun();service.LoadScene("Residents");while(service.IsTransitioning)yield return null; }
                var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);view.Restart(true);
                view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
                var card=view.statusCard;view.ShowResident(4);yield return null;
                Check(card.illustrated&&card.cells.Length==50&&card.thresholdMarkers.Length==5&&card.attitudes.Length==5,"Five live ten-cell bars, threshold markers and attitude faces are wired");
                Check(card.number.text=="202"&&card.preference.sprite==view.topicIcons[view.Run.residents[4].preferredTopic],"Room and preference come from the selected resident");
                Canvas.ForceUpdateCanvases();Check(card.number.preferredHeight<=card.number.rectTransform.rect.height,"Room number fits visibly inside its original pill");
                Check(card.portrait.sprite==view.residents[4].sprite,"Portrait matches the current scene animation frame");
                Check(card.coveredSidebar.All(o=>!o.activeSelf),"The covered diagram and prompt are hidden while observing");
                for(int floor=0;floor<=3;floor++)
                {
                    view.SelectFloor(floor);yield return null;view.ShowResident(floor==0?4:(3-floor)*3);yield return null;
                    Check(view.residents.Where(r=>r.gameObject.activeInHierarchy&&card.InArea(r.rectTransform).Overlaps(card.InArea(view.ResidentViewport))).All(r=>!card.CurrentRect.Overlaps(card.InArea(r.rectTransform))),"Card avoids visible residents in view "+floor);
                    Check(view.topics.All(b=>!card.CurrentRect.Overlaps(card.InArea((RectTransform)b.transform))),"All five recommendation controls remain unobstructed in view "+floor);
                    view.CancelRecommendation();
                }
                view.SelectFloor(0);view.ShowResident(4);var resident=view.Run.residents[4];
                // Explicit fixture covers zero/full/fractional pressure and all expressions without relying on chance.
                resident.saturation=new[]{0f,.23f,.70f,1f,.49f};resident.aversionThresholds=new[]{.25f,.43f,.70f,.95f,.52f};resident.interest=new[]{.8f,.2f,.8f,.2f,.8f};
                card.Refresh();
                Check(card.cells.Take(10).All(c=>c.fillAmount==0)&&card.cells.Skip(30).Take(10).All(c=>c.fillAmount==1),"Empty and full saturation draw zero and ten cells");
                Check(Mathf.Abs(card.cells[12].fillAmount-.3f)<.001f&&card.cells[13].fillAmount==0,"Fractional pressure fills only the partial last cell");
                Check(Enumerable.Range(0,5).All(t=>Mathf.Abs(card.thresholdMarkers[t].anchoredPosition.x-Mathf.Lerp(card.thresholdRange.x,card.thresholdRange.y,resident.aversionThresholds[t]))<.001f),"Every marker uses that resident and theme's individual threshold");
                Check(card.thresholdMarkers.Select(t=>t.anchoredPosition.x).Distinct().Count()==5,"Different thresholds do not collapse to a shared marker");
                Check(card.attitudes[0].Attitude==0&&card.attitudes[1].Attitude==1&&card.attitudes[2].Attitude==2&&card.overloadMarks[2].gameObject.activeSelf,"Happy, neutral and overloaded expressions reflect real data, including equality at the threshold");
                Check(resident.interest[2]>=.5f&&card.attitudes[2].Attitude==2,"Liking a topic and being tired of its recommendations coexist");
                Canvas.ForceUpdateCanvases();Check(card.details.preferredHeight<=card.details.rectTransform.rect.height,"Receipt and reaction fit at fixed readable font size");
                yield return Capture("Fixture_AllAttitudes");
                string before=JsonUtility.ToJson(resident);int eventCount=view.Run.events.Count;view.SelectTopic(3);
                Check(card.confirmLabel.text.Contains("新闻")&&!view.selectionLabel.gameObject.activeSelf,"Confirm names the prepared theme without a clipped duplicate heading");
                Check(JsonUtility.ToJson(resident)==before&&view.Run.events.Count==eventCount,"Preparing a theme does not change any resident state");
                float remaining=view.Run.Remaining;view.TickStage(20);Check(view.Run.Remaining==remaining&&card.pauseNote.text.Contains("时间暂停"),"Observation explicitly pauses world time");
                int passes=card.LayoutPassCount;for(int i=0;i<120;i++)card.Reposition();Check(card.LayoutPassCount-passes<=1,"Stable observation avoids repeated placement work");
                card.decline.onClick.Invoke();Check(!resident.recommended&&card.coveredSidebar.All(o=>o.activeSelf),"Decline consumes no opportunity and restores the full diagram");
                view.ShowResident(4);view.SelectTopic(0);Check(view.ConfirmRecommendation()&&!view.ConfirmRecommendation(),"Confirmation commits exactly once");
                view.ShowResident(4);Check(card.confirmLabel.text=="本阶段已推荐"&&!card.confirm.interactable,"Reopening accurately marks an already recommended resident");
                card.close.onClick.Invoke();Check(!card.gameObject.activeSelf&&resident.recommended,"The supplied close icon dismisses the card without undoing a recommendation");
                view.AdvanceStage();Check(view.Run.Started&&resident.recommended,"Teaching recommendation survives the manual start of stage one");
                view.Run.FinishEarly();view.Run.NextStage();var expected=view.Run.residents[4].saturation.ToArray();view.TickStage(0);view.ShowResident(4);card.Refresh();
                Check(Enumerable.Range(0,5).All(t=>Mathf.Abs(card.cells.Skip(t*10).Take(10).Sum(c=>c.fillAmount)/10-expected[t])<.001f),"Reopening after a stage boundary displays inherited pressure rather than resetting bars");
                view.CancelRecommendation();view.Restart(true);view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();view.ShowResident(0);
                var fresh=view.Run.residents[0];Check(card.number.text=="301"&&Mathf.Abs(card.cells.Take(10).Sum(c=>c.fillAmount)/10-fresh.saturation[0])<.001f,"New-game card binds fresh resident data");
                yield return Capture("NewGame_StatusCard");
                view.CancelRecommendation();checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/status-card-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);
            }
            finally { service.settings.motion=motion;service.Apply(false);service.ReplaceAchievementStoreForTest(original);PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();service.RequestNewRun(); }
            Destroy(this);
        }
    }
}
#endif
