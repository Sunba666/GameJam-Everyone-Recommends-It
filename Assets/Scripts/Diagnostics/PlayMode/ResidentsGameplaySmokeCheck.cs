#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class ResidentsGameplaySmokeCheck : MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        const string Dir="Builds/Verification/Residents";
        void Check(bool pass,string text)
        {
            results.Add((pass?"PASS ":"FAIL ")+text);
            File.WriteAllLines("Library/residents-gameplay-smoke.txt",results);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Dir+"/"+name+".png");
            yield return new WaitForEndOfFrame();
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);
            var service=FrontendServices.Instance;
            if(SceneManager.GetActiveScene().name!="Residents")
            {
                service.RequestNewRun(); service.LoadScene("Residents");
                while(service.IsTransitioning)yield return null;
            }
            yield return new WaitForSecondsRealtime(.3f);
            var view=FindObjectOfType<ResidentsView>(); view.SendMessage("OnApplicationFocus",true);
            Check(view && view.statusCard && view.stageProgress,"Resident card and stage HUD installed");
            Check(view.Run.stageDuration==90 && view.Run.totalStages==12,"90 seconds per stage and 12 stages");
            var first=new ResidentRun(10,90,12,100); var same=new ResidentRun(10,90,12,100); var other=new ResidentRun(10,90,12,101);
            Check(first.residents.Select(r=>r.preferredTopic).SequenceEqual(same.residents.Select(r=>r.preferredTopic)),"Same debug seed reproduces opening");
            Check(!first.residents.Select(r=>r.preferredTopic).SequenceEqual(other.residents.Select(r=>r.preferredTopic)),"Different seeds change resident preferences");
            Check(first.residents.All(r=>r.interest.Length==5 && r.saturation.Length==5 && r.saturation.All(x=>x>=0&&x<=1)),"All residents store five bounded saturation values");
            Check(!first.NextStage(),"Cannot skip a running stage");
            first.Recommend(0,2); var record=first.residents[0]; var values=(float[])record.saturation.Clone(); var interests=(float[])record.interest.Clone(); int preference=record.preferredTopic;
            first.Tick(89.99f); Check(!first.StageEnded && first.Remaining>0,"Stage remains live before 90-second boundary");
            first.Tick(1); Check(first.StageEnded && first.Remaining==0,"Timer clamps to zero and ends once");
            Check(!first.Recommend(0,2),"Time-up rejects recommendations");
            Check(first.NextStage() && first.Stage==2 && first.Remaining==90,"Next stage resets clock to 90");
            Check(ReferenceEquals(record,first.residents[0]) && values.SequenceEqual(record.saturation) && interests.SequenceEqual(record.interest) && preference==record.preferredTopic,"Next stage preserves the same record, preferences and all saturation values");
            while(!first.Finished) { first.Tick(100); if(!first.Finished)first.NextStage(); }
            Check(first.Stage==12 && !first.NextStage(),"Run ends at stage 12 without stage 13");
            var mood=first.residents[0]; mood.saturation[0]=.7f;
            Check(mood.Attitude(0)==2,"Saturation threshold produces aversion expression");

            view.RestartRun();
            Check(view.Run.residents.Select((r,i)=>view.statusIcons[i].sprite==view.topicIcons[r.preferredTopic]).All(x=>x),"All ten schematic icons match actual preferences");
            yield return Capture("01_RandomOpening");
            bool popup=true,overlap=false,inside=true,raycasts=true,contents=true;
            for(int i=0;i<view.residents.Length;i++)
            {
                var button=view.residents[i].GetComponent<Button>(); button.onClick.Invoke();
                var card=view.statusCard; var state=view.Run.residents[i];
                popup&=card.gameObject.activeSelf && card.SelectedResident==i;
                inside&=card.placementArea.rect.Contains(card.CurrentRect.min) && card.placementArea.rect.Contains(card.CurrentRect.max);
                overlap|=view.residents.Any(r=>card.CurrentRect.Overlaps(card.InArea(r.rectTransform)));
                overlap|=card.reservedAreas.Any(r=>card.CurrentRect.Overlaps(card.InArea(r)));
                contents&=card.preference.sprite==view.statusIcons[i].sprite && card.portrait.sprite==view.residents[i].sprite && card.number.text==(i+1).ToString("00");
                for(int topic=0;topic<5;topic++)
                {
                    int count=card.cells.Skip(topic*10).Take(10).Count(x=>x.enabled);
                    contents&=count==Mathf.RoundToInt(state.saturation[topic]*10) && card.attitudes[topic].Attitude==state.Attitude(topic);
                }
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,view.residents[i].transform.position)};
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                raycasts&=hits.Count>0 && hits[0].gameObject==view.residents[i].gameObject;
                if(i==1)yield return Capture("02_CardMiddleResident");
                if(i==9)yield return Capture("03_CardLowerResident");
            }
            Check(popup,"All ten residents open their own cards");
            Check(!overlap && inside,"All ten popup positions avoid every resident, side panel, timer and footer; stay inside screen artboard");
            Check(raycasts,"Resident centers remain clickable with popup open");
            Check(contents,"Portrait, preferred icon, ten-cell bars and expressions match live data");
            Check(view.statusCard.attitudes.All(x=>x.GetComponent<CanvasRenderer>()),"All attitude expressions have a CanvasRenderer");
            view.statusCard.close.onClick.Invoke(); Check(!view.statusCard.gameObject.activeSelf,"Card close button works");
            view.statusIcons[0].GetComponent<Button>().onClick.Invoke(); Check(view.statusCard.SelectedResident==0,"Schematic icon also opens the matching resident");
            view.statusCard.Hide();
            float old=view.Run.residents[0].saturation[0]; view.SelectTopic(0);
            Check(view.Run.residents[0].saturation[0]>old,"Recommending a topic updates real saturation");
            view.ShowResident(0); yield return Capture("04_LiveSaturation"); view.statusCard.Hide();
            view.settingsButton.onClick.Invoke(); float before=view.Run.Remaining;
            view.TickStage(20); Check(view.Run.Remaining==before,"Settings pauses the stage clock");
            view.settingsPanel.GetComponent<SettingsView>().Hide(); view.SendMessage("OnApplicationFocus",false); before=view.Run.Remaining;
            view.TickStage(20); Check(view.Run.Remaining==before,"Losing application focus pauses the stage clock");
            view.SendMessage("OnApplicationFocus",true);
            float startCursor=view.stageProgress.cursor.anchoredPosition.x; view.TickStage(30);
            Check(view.stageProgress.cursor.anchoredPosition.x<startCursor && view.stageProgress.fill.fillAmount<1,"Progress right edge and cursor move left as time elapses");
            yield return Capture("05_TimerRunning");
            var states=view.Run.residents.Select(r=>(float[])r.saturation.Clone()).ToArray(); var ids=view.Run.residents.Select(r=>r.preferredTopic).ToArray();
            view.TickStage(100);
            Check(view.Run.StageEnded && view.topics.All(x=>!x.interactable) && view.stageProgress.next.gameObject.activeSelf,"Time-up disables recommendations and reveals next-stage button");
            yield return Capture("06_StageEnded");
            view.stageProgress.next.onClick.Invoke();
            Check(view.Run.Stage==2 && view.topics.All(x=>x.interactable) && states.Select((x,i)=>x.SequenceEqual(view.Run.residents[i].saturation)).All(x=>x) && ids.SequenceEqual(view.Run.residents.Select(r=>r.preferredTopic)),"Actual next-stage button keeps all preferences/saturation and resumes play");
            var oldRun=view.Run; int oldSeed=oldRun.seed;
            service.LoadScene("MainMenu"); while(service.IsTransitioning)yield return null;
            Check(ReferenceEquals(service.CurrentRun,oldRun),"Returning to menu does not mutate the stored run");
            FindObjectOfType<MenuView>().start.onClick.Invoke(); while(service.IsTransitioning)yield return null;
            view=FindObjectOfType<ResidentsView>();
            Check(view.Run.seed!=oldSeed && view.Run.Stage==1 && view.Run.Remaining>88,"Starting from menu creates a fresh random run at stage 1");
            results.Add("COMPLETE "+DateTime.Now.ToString("O")); File.WriteAllLines("Library/residents-gameplay-smoke.txt",results);
            File.WriteAllLines(Dir+"/PlayModeChecks.txt",results); Destroy(this);
        }
    }
}
#endif
