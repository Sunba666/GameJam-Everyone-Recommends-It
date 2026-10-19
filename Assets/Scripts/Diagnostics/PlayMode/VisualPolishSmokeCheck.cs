#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class VisualPolishSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/VisualPolish";
        readonly List<string> results=new List<string>();
        void Check(bool valid,string message)
        {
            results.Add((valid?"PASS ":"FAIL ")+message);File.WriteAllLines("Library/visual-polish-smoke.txt",results);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame();
        }
        bool ButtonsInside(DemoOverlay overlay)
        {
            Rect safe=overlay.paper.rect;safe.xMin+=40;safe.xMax-=40;safe.yMin+=40;safe.yMax-=40;
            foreach(var button in overlay.buttons.Where(b=>b.gameObject.activeSelf))
            {
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                if(corners.Any(c=>!safe.Contains(overlay.paper.InverseTransformPoint(c))))return false;
                var label=button.GetComponentInChildren<Text>();
                if(label.rectTransform.rect.width>((RectTransform)button.transform).rect.width-12)return false;
            }
            return true;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
            var service=FrontendServices.Instance;bool originalMotion=service.settings.motion;
            view.Restart(true);view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
            Check(view.delivery&&view.delivery.arrow.GetComponent<CanvasRenderer>()&&view.delivery.receiptIcons.Length==9,"Real Canvas arrow and nine receipt badges are installed");
            int delivered=view.delivery.DeliveryCount,eventCount=view.Run.events.Count;
            int incoming=(view.Run.residents[0].preferredTopic+1)%5;
            view.ShowResident(0);view.SelectTopic(incoming);
            Check(delivered==view.delivery.DeliveryCount&&eventCount==view.Run.events.Count&&!view.delivery.ArrowVisible,"Preparing a topic has no delivery, animation or state mutation");
            yield return Capture("01_ObservationLayout");
            view.CancelRecommendation();Check(delivered==view.delivery.DeliveryCount&&!view.Run.residents[0].recommended,"Cancel preserves opportunity and does not play a receipt");
            view.ShowResident(0);view.SelectTopic(incoming);view.ConfirmRecommendation();
            Check(view.delivery.DeliveryCount==delivered+1&&view.delivery.LastResident==0&&view.delivery.LastTopic==incoming,"Exactly one receipt uses the submitted resident and topic");
            yield return null; // The presentation queue owns when its receipt appears.
            Check(view.delivery.receiptIcons[0].sprite==view.topicIcons[incoming]&&view.delivery.receiptIcons[0].gameObject.activeSelf,"Left badge shows incoming content independently of current preference");
            view.ConfirmRecommendation();Check(view.delivery.DeliveryCount==delivered+1,"Repeat confirm never duplicates the delivery effect");
            yield return new WaitForSecondsRealtime(.36f);
            Check(view.delivery.ArrowVisible,"Actual recommendation arrow is visible while the teaching clock is frozen");
            yield return Capture("02_DeliveryArrow");
            yield return new WaitForSecondsRealtime(.8f);
            Check(!view.delivery.ArrowVisible&&view.Run.Remaining==90,"Arrow completes without advancing the tutorial world clock");
            view.AdvanceStage();service.settings.motion=false;service.Apply(false);
            view.ShowResident(1);view.SelectTopic(4);view.ConfirmRecommendation();yield return null;
            Check(view.delivery.receiptIcons[1].gameObject.activeSelf&&view.delivery.receiptIcons[1].rectTransform.localScale==Vector3.one,"Motion disabled still provides a static incoming-topic receipt");
            service.settings.motion=originalMotion;service.Apply(false);view.SendMessage("OnApplicationFocus",false);
            for(int t=0;t<5;t++)view.Run.residents[4].propsUntil[t]=view.Run.Stage+2;
            view.RenderStates(view.Run.residents);view.SelectFloor(0);
            Check(view.propImages.All(p=>p.rectTransform.sizeDelta.x>=68)&&view.propImages[0].rectTransform.sizeDelta.x==128,"All props are enlarged by category, preserving sprite aspect");
            Check(view.propImages.Skip(20).Take(5).Select(p=>p.rectTransform.anchoredPosition).Distinct().Count()==5,"Five simultaneous retained props occupy separate positions");
            yield return Capture("03_LargerProps");
            view.ShowSummary();Check(ButtonsInside(view.overlay),"Summary actions remain aligned inside the paper with 40px safe margin");
            yield return Capture("04_SummaryLayout");
            int events=view.Run.events.Count;delivered=view.delivery.DeliveryCount;
            view.BeginReplay(false);view.ReplayStep(1);
            Check(view.Run.events.Count==events&&view.delivery.DeliveryCount==delivered&&!view.delivery.ArrowVisible,"Read-only replay neither sends nor plays a live delivery");
            Check(ButtonsInside(view.overlay),"Replay actions fit the narrower side panel");yield return Capture("05_ReplayLayout");
            view.overlay.Show("长记录排版检查",string.Join("\n\n",Enumerable.Repeat("住户收到来自邻居的推荐，正在考虑继续观看或者暂时换一个话题。",22)),new[]{"返回总结","继续观察"},new Action[]{view.ShowSummary,()=>view.overlay.Hide()});
            Check(view.overlay.reading.vertical&&view.overlay.readingViewport.GetComponent<RectMask2D>()&&view.overlay.body.rectTransform.rect.height>view.overlay.readingViewport.rect.height,"Long records scroll within a masked reading area instead of overflowing into actions");
            Check(ButtonsInside(view.overlay),"Two actions are centered with consistent safe padding");yield return Capture("06_LongRecordLayout");
            Canvas.ForceUpdateCanvases();view.overlay.reading.verticalNormalizedPosition=0;yield return null;
            Check(view.overlay.body.rectTransform.anchoredPosition.y>0,"Scrollable records actually move to the final lines");
            yield return Capture("06b_LongRecordBottom");
            view.Restart(true);Check(ButtonsInside(view.overlay),"Six goal options use two centered rows inside the panel");yield return Capture("07_GoalLayout");
            view.overlay.Hide();view.settingsPanel.SetActive(true);yield return new WaitForSecondsRealtime(.7f);yield return Capture("08_SettingsLayout");view.settingsPanel.SetActive(false);
            var dump=view.GetComponentsInChildren<Text>(true).Where(t=>!t.transform.IsChildOf(view.buildingContent)).Select(t=>t.name+"|"+t.text.Replace('\n',' ')+"|rect="+t.rectTransform.rect+"|pos="+t.rectTransform.anchoredPosition+"|font="+t.fontSize);
            File.WriteAllLines(Folder+"/TextLayout.txt",dump);
            view.Restart(true);view.SendMessage("OnApplicationFocus",true);
            results.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/visual-polish-smoke.txt",results);File.WriteAllLines(Folder+"/Checks.txt",results);Destroy(this);
        }
    }
}
#endif
