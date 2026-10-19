#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class FloorDetailSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/FloorDetails";
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string label) { checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Library/floor-detail-smoke.txt",checks); }
        static string State(ResidentRun run)=>run.Stage+"|"+run.events.Count+"|"+string.Join(";",run.residents.Select(r=>r.preferredTopic+":"+r.recommended+":"+string.Join(",",r.interest)+":"+string.Join(",",r.saturation)+":"+string.Join(",",r.aversionThresholds)));
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            if(SceneManager.GetActiveScene().name!="Residents") { var services=FrontendServices.Instance;services.RequestNewRun();services.LoadScene("Residents");while(services.IsTransitioning)yield return null; }
            var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);var detail=view.floorDetails;
            Check(detail&&detail.definitions.Length==3&&detail.definitions.All(d=>d&&d.day&&d.night),"Three floors have separate generated day and night artwork");
            Check(detail.definitions.All(d=>d.day.texture.width==d.night.texture.width&&d.day.texture.height==d.night.texture.height),"Day/night pairs retain identical canvas dimensions");
            Check(detail.anchors.Length==39&&detail.anchors.Select(a=>a.stableId).Distinct().Count()==39,"Thirty-nine stable room/interaction placement sockets have unique IDs");
            view.Restart(true);view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
            var originals=view.residents.Select(r=>r.rectTransform.anchoredPosition).ToArray();var instanceIds=view.residents.Select(r=>r.GetInstanceID()).ToArray();
            view.ShowResident(0);view.SelectTopic(0);view.ConfirmRecommendation();view.AdvanceStage();view.TogglePause();
            for(int floor=1;floor<=3;floor++)
            {
                string before=State(view.Run);view.floors[floor-1].onClick.Invoke();yield return new WaitForSecondsRealtime(.7f);
                Check(detail.IsOpen&&!detail.IsTransitioning&&detail.CurrentFloor==floor&&detail.dayImage.sprite==detail.definitions[floor-1].day,"Floor "+floor+" button enters its detailed daytime interior");
                var visible=view.residents.Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Check(visible.Length==3&&visible.All(r=>ResidentRun.FloorOf(Array.IndexOf(view.residents,r))==floor),"Detailed floor "+floor+" displays exactly its original three residents");
                Check(State(view.Run)==before&&view.residents.Select(r=>r.GetInstanceID()).SequenceEqual(instanceIds),"Entering floor "+floor+" preserves state and reuses existing actor objects");
                Check(detail.FindAnchor((floor*100+1)+"/Tabletop")&&detail.FindAnchor((floor*100+2)+"/Floor")&&detail.FindAnchor((floor*100+3)+"/Wall"),"Floor "+floor+" exposes stable tabletop, floor and wall attachment points");
                yield return Capture("Floor"+floor+"_Day_Runtime");
            }
            view.SelectFloor(1);view.SelectFloor(3);yield return new WaitForSecondsRealtime(.65f);
            Check(detail.CurrentFloor==3&&view.residents.Count(r=>r.gameObject.activeInHierarchy)==3,"Rapid floor changes finish at the last selected floor without duplicate residents");
            bool motion=FrontendServices.Instance.settings.motion;FrontendServices.Instance.settings.motion=false;
            view.SelectFloor(1);Check(detail.CurrentFloor==1&&!detail.IsTransitioning&&detail.stage.localScale==Vector3.one,"Reduced-motion floor entry skips zoom and completes immediately");
            FrontendServices.Instance.settings.motion=motion;
            var anchorPositions=detail.anchors.ToDictionary(a=>a.stableId,a=>a.Rect.anchoredPosition);
            view.Run.Tick(48);view.TickStage(0);yield return new WaitForSecondsRealtime(.9f);
            for(int floor=1;floor<=3;floor++)
            {
                view.SelectFloor(floor);yield return new WaitForSecondsRealtime(.7f);
                Check(detail.CurrentDefinition.night==detail.nightImage.sprite&&detail.NightWeight>.98f,"Floor "+floor+" uses its paired illuminated night interior");yield return Capture("Floor"+floor+"_Night_Runtime");
            }
            Check(detail.anchors.All(a=>anchorPositions[a.stableId]==a.Rect.anchoredPosition),"Night changes never move interaction attachment coordinates");
            view.SelectFloor(2);yield return new WaitForSecondsRealtime(.65f);view.TogglePause();
            int actor=4;view.residents[actor].GetComponent<Button>().onClick.Invoke();
            Check(view.statusCard.SelectedResident==actor&&view.PausedForWorld,"Clicking an enlarged actor opens its real observation card and freezes world time");
            var card=view.statusCard;Check(view.residents.Where(r=>r.gameObject.activeInHierarchy).All(r=>!card.CurrentRect.Overlaps(card.InArea(r.rectTransform))),"Observation card avoids all three enlarged residents");
            yield return Capture("Detail_ObservationCard");
            string state=State(view.Run);view.SelectTopic(4);Check(State(view.Run)==state,"Selecting content inside a detailed room does not send or consume");
            Check(view.ConfirmRecommendation()&&!view.ConfirmRecommendation()&&view.Run.residents[actor].recommended,"Detailed-room confirmation sends once and keeps the original opportunity rule");
            yield return new WaitForSecondsRealtime(.35f);yield return Capture("Detail_Recommendation");
            view.TogglePause();string history=State(view.Run);view.allFloors.onClick.Invoke();yield return new WaitForSecondsRealtime(.65f);
            Check(!detail.IsOpen&&view.residents.All(r=>r.gameObject.activeInHierarchy)&&view.buildingViewport.gameObject.activeSelf,"All-floors control restores the full nine-resident building view");
            Check(originals.SequenceEqual(view.residents.Select(r=>r.rectTransform.anchoredPosition))&&view.residents.All(r=>r.transform.parent==view.buildingContent),"Returning restores every original full-building actor coordinate and parent");
            Check(State(view.Run)==history,"Returning to the building does not reset preference, pressure, thresholds or history");
            view.TogglePause();view.SelectFloor(1);yield return new WaitForSecondsRealtime(.65f);float time=view.Run.Remaining;view.TickStage(.5f);
            Check(view.Run.Remaining<time,"Ordinary floor switching and detail viewing do not pause the stage clock");
            view.Run.FinishEarly();view.ShowSummary();int events=view.Run.events.Count;view.BeginReplay(false);yield return new WaitForSecondsRealtime(.75f);
            Check(detail.IsOpen&&detail.CurrentFloor==3&&detail.NightWeight<.02f&&view.Run.events.Count==events,"Replay enters the recorded resident's room in the correct daylight without altering history");yield return Capture("Detail_ReadOnlyReplay");
            view.ShowSummary();view.Restart(true);
            Check(!detail.IsOpen&&view.residents.All(r=>r.gameObject.activeInHierarchy)&&view.Run.Stage==1,"Retry restores the complete building and fresh presentation without extra residents");
            checks.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/floor-detail-smoke.txt",checks);File.WriteAllLines(Folder+"/Checks.txt",checks);Destroy(this);
        }
    }
}
#endif
