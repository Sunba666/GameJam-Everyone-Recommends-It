#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    public sealed class BackgroundLightingSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/Background";
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string label) { checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Library/background-smoke.txt",checks); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
            var tint=view.buildingTint.rectTransform;var art=(RectTransform)tint.parent;
            Check(art.name=="Artboard_1672x941"&&!tint.IsChildOf(view.buildingViewport),"Whole-background lighting is outside the clipped/moving floor viewport");
            Check(tint.sizeDelta==new Vector2(1228,941)&&tint.anchoredPosition==new Vector2(-220,0),"One continuous rectangle covers the entire sky-to-pavement building image");
            Check(tint.GetSiblingIndex()>art.Find("FixedBuildingHeader").GetSiblingIndex()&&tint.GetSiblingIndex()>art.Find("FixedBuildingFooter").GetSiblingIndex()&&tint.GetSiblingIndex()<view.paceButton.transform.GetSiblingIndex(),"Header/footer receive the same lighting while controls stay above it");
            Check(!view.buildingTint.raycastTarget,"Lighting does not intercept resident or button clicks");
            view.Restart(true);view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();view.ShowResident(0);view.SelectTopic(0);view.ConfirmRecommendation();view.AdvanceStage();view.TogglePause();
            yield return new WaitForSecondsRealtime(.8f);Check(view.BackgroundTimeOfDay=="白天"&&view.buildingTint.color.a<.01f,"Daylight restores the whole background to its original appearance");yield return Capture("01_Day");
            view.Run.Tick(45);view.TickStage(0);yield return new WaitForSecondsRealtime(.8f);
            Check(view.BackgroundTimeOfDay=="黄昏"&&Mathf.Abs(view.buildingTint.color.a-.16f)<.01f,"Dusk changes the entire background with one warm transition");yield return Capture("02_Dusk");
            view.Run.Tick(3);view.TickStage(0);yield return new WaitForSecondsRealtime(.8f);
            Check(view.BackgroundTimeOfDay=="晚上"&&Mathf.Abs(view.buildingTint.color.a-.33f)<.01f,"Night changes sky, apartments and ground together");yield return Capture("03_Night");
            Vector2 position=tint.anchoredPosition;Vector2 size=tint.sizeDelta;
            for(int floor=1;floor<=3;floor++) { view.SelectFloor(floor);yield return new WaitForSecondsRealtime(.2f); }
            Check(tint.anchoredPosition==position&&tint.sizeDelta==size&&tint.localScale==Vector3.one,"Switching floors cannot move or crop the full-background lighting layer");
            yield return Capture("04_NightFloor3");view.SelectFloor(0);view.Run.FinishEarly();view.ShowSummary();int events=view.Run.events.Count;
            view.BeginReplay(false);yield return new WaitForSecondsRealtime(.8f);
            Check(view.BackgroundTimeOfDay=="白天"&&view.buildingTint.color.a<.01f&&view.Run.events.Count==events,"Replaying an early event shows its daylight without mutating the finished run");yield return Capture("05_DayReplay");
            view.ShowSummary();yield return new WaitForSecondsRealtime(.8f);
            Check(view.BackgroundTimeOfDay=="晚上"&&Mathf.Abs(view.buildingTint.color.a-.33f)<.01f,"Leaving replay restores the actual night lighting");
            view.ContinueFromSummary();yield return new WaitForSecondsRealtime(.8f);
            Check(view.BackgroundTimeOfDay=="白天"&&view.buildingTint.color.a<.01f&&view.Run.Stage==2,"The next stage restores the entire image to daylight");
            view.Restart(true);checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/background-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);Destroy(this);
        }
    }
}
#endif
