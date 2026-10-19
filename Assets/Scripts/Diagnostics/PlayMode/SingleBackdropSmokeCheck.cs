#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EveryoneRecommends.Frontend
{
    public sealed class SingleBackdropSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/SingleBackdrop";
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string text) { checks.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines("Library/single-backdrop-smoke.txt",checks); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        Color Pixel(Texture2D image,ApartmentBackdrop backdrop,float x,float y)
        {
            Vector3 world=backdrop.background.rectTransform.TransformPoint(new Vector3(x-836,470.5f-y,0));
            Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,world);
            return image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(screen.x),0,image.width-1),Mathf.Clamp(Mathf.RoundToInt(screen.y),0,image.height-1));
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);var backdrop=view.apartmentBackdrop;
            var art=view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Artboard_1672x941");
            Check(backdrop&&backdrop.background&&AssetDatabase.GetAssetPath(backdrop.background.sprite)=="Assets/Art/Building/Building_CompleteDay_v2.png","The building uses one complete clean background sprite");
            Check(!art.Find("FixedBuildingHeader")&&!art.Find("FixedBuildingFooter"),"Old fixed header/footer picture strips are absent");
            Check(view.buildingViewport.sizeDelta==new Vector2(1228,941)&&view.buildingContent.anchoredPosition==new Vector2(220,0),"Camera viewport spans the whole image height instead of a middle slice");
            Check(!view.buildingTint.gameObject.activeSelf&&!backdrop.background.raycastTarget,"Uniform dark overlay is disabled and background does not steal clicks");
            Check(backdrop.ActiveMaterial.shader.isSupported&&!ShaderUtil.ShaderHasError(backdrop.ActiveMaterial.shader),"Lamp lighting shader compiles and is supported by the actual renderer");
            var positions=view.residents.Select(r=>r.rectTransform.anchoredPosition).ToArray();
            view.Restart(true);view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();view.ShowResident(0);view.SelectTopic(0);view.ConfirmRecommendation();view.AdvanceStage();view.TogglePause();
            yield return new WaitForSecondsRealtime(1);
            Check(backdrop.NightBlend<.01f&&backdrop.DuskBlend<.01f,"Day uses the untouched daytime colors");yield return Capture("01_CompleteDay");
            yield return new WaitForEndOfFrame();var day=ScreenCapture.CaptureScreenshotAsTexture();Color dayOutside=Pixel(day,backdrop,60,400);Destroy(day);
            view.Run.Tick(45);view.TickStage(0);yield return new WaitForSecondsRealtime(1);
            Check(backdrop.DuskBlend>.98f&&backdrop.NightBlend<.01f,"Dusk is a continuous warm transition on the full sprite");yield return Capture("02_CompleteDusk");
            view.Run.Tick(3);view.TickStage(0);yield return new WaitForSecondsRealtime(1.1f);
            Check(backdrop.NightBlend>.98f&&backdrop.DuskBlend<.02f,"Night lighting runs on the complete background material");
            yield return new WaitForEndOfFrame();var night=ScreenCapture.CaptureScreenshotAsTexture();Color outside=Pixel(night,backdrop,60,400),bulb=Pixel(night,backdrop,1016,216),underLamp=Pixel(night,backdrop,1016,257);Destroy(night);
            Check(outside.grayscale<dayOutside.grayscale*.65f,"Actual rendered exterior becomes substantially darker at night");
            Check(bulb.grayscale>outside.grayscale+.22f&&underLamp.r>underLamp.b,"Actual lamp bulb stays bright and illuminates a warm region beneath it");
            File.WriteAllText(Folder+"/RenderedLighting.txt","DayExterior="+dayOutside+"\nNightExterior="+outside+"\nLampBulb="+bulb+"\nUnderLamp="+underLamp);
            Check(view.residents.All(r=>r.color.r>.9f&&r.color.g>.8f),"Residents stay readable under warm indoor light instead of uniform outdoor darkness");yield return Capture("03_CompleteNight");
            for(int floor=1;floor<=3;floor++) { view.SelectFloor(floor);yield return new WaitForSecondsRealtime(.65f); }
            Check(positions.SequenceEqual(view.residents.Select(r=>r.rectTransform.anchoredPosition))&&backdrop.background.transform.IsChildOf(view.buildingContent),"All residents and the one-piece background move in the same camera space");yield return Capture("04_NightFloor3");
            view.SelectFloor(0);view.Run.FinishEarly();view.ShowSummary();int eventCount=view.Run.events.Count;view.BeginReplay(false);yield return new WaitForSecondsRealtime(1);
            Check(view.BackgroundTimeOfDay=="白天"&&backdrop.NightBlend<.01f&&view.Run.events.Count==eventCount,"Replay restores recorded daylight without changing the simulation");yield return Capture("05_DaylightReplay");
            view.ShowSummary();view.ContinueFromSummary();yield return new WaitForSecondsRealtime(1);
            Check(view.Run.Stage==2&&backdrop.NightBlend<.01f,"The next stage returns to normal daylight");view.Restart(true);
            checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/single-backdrop-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);Destroy(this);
        }
    }
}
#endif
