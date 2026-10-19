#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class DemoSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/Demo";
        readonly List<string> results=new List<string>();
        void Check(bool pass,string name)
        {
            results.Add((pass?"PASS ":"FAIL ")+name);File.WriteAllLines("Library/demo-smoke.txt",results);
        }
        static string State(ResidentRun run)=>run.Stage+"|"+run.Remaining+"|"+run.events.Count+"|"+string.Join(";",run.residents.Select(r=>r.preferredTopic+":"+r.recommended+":"+string.Join(",",r.interest)+":"+string.Join(",",r.saturation)));
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForEndOfFrame();
        }
        static ResidentRun Strategy(int seed,int mode)
        {
            var run=new ResidentRun(9,90,12,seed);run.SetTarget(0);run.Recommend(0,0);run.StartStage();
            while(true)
            {
                for(int i=0;i<9;i++)if(mode!=2||run.Stage<=3)run.Recommend(i,mode==1?(run.Stage-1)%5:0);
                run.FinishEarly();if(run.Finished)break;run.NextStage();
            }
            return run;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            var run=new ResidentRun(9,90,12,411);
            Check(!run.Recommend(0,0)&&!run.StartStage(),"Cannot send or start before selecting a goal");
            Check(run.SetTarget(0)&&!run.SetTarget(1),"Goal confirmation locks once");
            Check(run.Recommend(0,0)&&run.TutorialDone&&!run.Started&&run.Remaining==90,"Tutorial uses a normal opportunity while clock stays paused");
            int count=run.events.Count;Check(!run.Recommend(0,0)&&!run.Recommend(1,1)&&run.events.Count==count,"No duplicate or second tutorial recommendation");
            Check(run.StartStage()&&run.residents[0].recommended,"Starting stage keeps tutorial opportunity consumed");
            Check(run.Recommend(1,1)&&!run.Recommend(1,2),"One direct recommendation per resident across day and night");
            run.Tick(45);Check(run.TimeOfDay=="黄昏","Dusk occurs at 45 seconds");
            var shares=run.events.Where(e=>e.kind=="邻居分享").ToArray();
            Check(shares.GroupBy(e=>e.source).All(g=>g.Count()==1)&&shares.GroupBy(e=>e.resident).All(g=>g.Count()==1),"At most one outgoing and incoming neighbour share per resident");
            Check(shares.All(e=>e.source>=0&&ResidentRun.FloorOf(e.source)==ResidentRun.FloorOf(e.resident)),"Neighbour sources use fixed same-floor relations");
            Check(run.residents.Count(r=>r.recommended)==2,"Neighbour shares do not consume player opportunities");
            run.Tick(3);Check(run.TimeOfDay=="晚上"&&run.residents[0].recommended,"Night begins at 48 seconds without resetting opportunities");
            run.FinishEarly();Check(run.StageEnded&&run.Remaining==0&&run.events.Count(e=>e.kind=="黄昏")==1,"Fast-forward finishes remaining simulation and dusk exactly once");
            var saturation=run.residents.Select(r=>(float[])r.saturation.Clone()).ToArray();var interests=run.residents.Select(r=>(float[])r.interest.Clone()).ToArray();
            Check(run.NextStage()&&run.Remaining==90&&run.residents.All(r=>!r.recommended)&&saturation.Select((r,i)=>r.SequenceEqual(run.residents[i].saturation)).All(x=>x)&&interests.Select((r,i)=>r.SequenceEqual(run.residents[i].interest)).All(x=>x),"Next stage preserves state and resets only time/opportunities");
            var a=Strategy(741,0);var b=Strategy(741,0);var rotate=Strategy(741,1);var rest=Strategy(741,2);
            Check(State(a)==State(b)&&a.events.Select(e=>e.text).SequenceEqual(b.events.Select(e=>e.text)),"Same seed and virtual action timing reproduce events and state");
            Check(State(a)!=State(rotate)&&State(a)!=State(rest),"Continuous push, topic rotation and stopping yield different real states");
            Check(a.Finished&&a.Stage==12&&!a.NextStage(),"Final result stops at stage 12");
            for(int i=0;i<9;i++)for(int t=0;t<5;t++) { a.residents[i].interest[t]=t==0?.9f:.1f;a.residents[i].saturation[t]=0; }
            Check(a.TargetWon()&&a.Scores()[0]==9,"Final score reads nine actual residents without drawing random victory");
            for(int i=0;i<9;i++)for(int t=0;t<5;t++)a.residents[i].interest[t]=t==i/3?.9f:.1f;
            Check(!a.TargetWon(),"A tied first place is not a win");
            var originalFrames=new[]{"Cat","Food","Game","News","Music"}.Select(id=>"Assets/EveryoneRecommends/ResidentMotion/"+id+"/Frames").ToArray();
            var spriteFiles=AssetDatabase.FindAssets("t:Sprite",originalFrames);
            Check(spriteFiles.Length==605,"All 605 original motion frames remain imported alongside optional additions");
            bool clips=true;long bytes=0;var textures=new HashSet<Texture2D>();
            foreach(var id in new[]{"Cat","Food","Game","News","Music"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/EveryoneRecommends/ResidentMotion/"+id+"/"+id+"_UI.anim");
                var curve=AnimationUtility.GetObjectReferenceCurve(clip,AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
                clips&=clip.frameRate==24&&Math.Abs(clip.length-121f/24)<.01f&&curve.Length>=121;
                foreach(var key in curve)
                {
                    var sprite=key.value as Sprite;clips&=sprite&&sprite.texture.width<=256&&sprite.texture.filterMode==FilterMode.Bilinear;
                    if(sprite&&textures.Add(sprite.texture))bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(sprite.texture);
                }
            }
            Check(clips,"Five clips retain 24 fps, full sequence, 5.04 second loops and smooth import");
            File.WriteAllText(Folder+"/MotionMemory.txt","Textures="+textures.Count+"\nRuntimeTextureMiB="+(bytes/1048576f).ToString("F2"));
            var service=FrontendServices.Instance;
            if(SceneManager.GetActiveScene().name!="Residents") { service.RequestNewRun();service.LoadScene("Residents");while(service.IsTransitioning)yield return null; }
            yield return new WaitForSecondsRealtime(.3f);
            var view=FindObjectOfType<ResidentsView>();view.SendMessage("OnApplicationFocus",true);
            Check(view.overlay&&view.motions.Length==9&&view.statusCard.details,"Complete gameplay UI and nine animation drivers installed");
            yield return Capture("01_Introduction");
            view.overlay.buttons[1].onClick.Invoke();view.overlay.buttons[0].onClick.Invoke();view.overlay.buttons[5].onClick.Invoke();
            Check(view.Run.Target==0&&!view.overlay.gameObject.activeSelf&&!view.Run.Started,"Actual skip/select/confirm goal flow stays in preparation");
            view.ShowResident(0);string before=State(view.Run);view.SelectTopic(0);
            Check(State(view.Run)==before&&view.CanConfirm,"Selecting a topic highlights only, without delivery or consumption");
            yield return Capture("02_ObservationAndSelection");
            view.CancelRecommendation();Check(State(view.Run)==before&&!view.statusCard.gameObject.activeSelf,"Cancelling an unsubmitted choice preserves state");
            view.ShowResident(0);view.SelectTopic(0);Check(view.ConfirmRecommendation()&&!view.ConfirmRecommendation(),"Actual confirm closes panel and atomically submits once");
            Check(view.Run.Remaining==90&&view.Run.residents[0].recommended,"Teaching click records reaction and keeps clock at 90");
            view.stageProgress.next.onClick.Invoke();Check(view.Run.Started&&view.Run.residents[0].recommended,"Actual start button keeps teaching results");
            float clock=view.Run.Remaining;view.ShowResident(1);view.TickStage(10);Check(view.Run.Remaining==clock,"Observation pauses world simulation");
            view.CancelRecommendation();view.TogglePause();view.TickStage(10);Check(view.Run.Remaining==clock,"Manual pause prevents clock and simulation");view.TogglePause();
            int oldFloor=view.SelectedFloor;before=State(view.Run);view.SelectFloor(2);Check(State(view.Run)==before&&view.SelectedFloor==2,"Floor switching changes view only");view.SelectFloor(0);
            bool animation=true;
            int original=view.Run.residents[0].preferredTopic;
            for(int topic=0;topic<5;topic++)
            {
                view.Run.residents[0].preferredTopic=topic;view.RenderStates(view.Run.residents);Sprite first=view.residents[0].sprite;
                yield return new WaitForSecondsRealtime(.18f);animation&=view.residents[0].sprite!=first&&view.motions[0].Topic==topic;
            }
            Check(animation,"All five provided animations change actual rendered sprites");
            bool motion=service.settings.motion;service.settings.motion=false;service.Apply(false);yield return null;
            Sprite still=view.residents[0].sprite;yield return new WaitForSecondsRealtime(.1f);
            Check(!view.motions[0].animator.enabled&&view.residents[0].sprite==still,"Motion setting disables resident animations with static fallback");
            service.settings.motion=motion;service.Apply(false);view.Run.residents[0].preferredTopic=original;view.RenderStates(view.Run.residents);
            yield return Capture("03_ResidentsAnimating");
            view.Run.FinishEarly();view.ShowSummary();before=State(view.Run);view.BeginReplay(false);view.ReplayStep(1);view.ReplayStep(-1);
            Check(State(view.Run)==before,"Replay navigation is read-only and does not reroll or resend");yield return Capture("04_ReadOnlyReplay");
            view.ShowSummary();view.ContinueFromSummary();Check(view.Run.Stage==2&&view.Run.Started,"Stage summary continues to the next playable stage");
            while(!view.Run.Finished) { view.Run.FinishEarly();if(!view.Run.Finished)view.Run.NextStage(); }
            view.ShowFinal();Check(view.overlay.gameObject.activeSelf&&view.overlay.body.text.Contains("本局目标")&&view.Run.Stage==12,"Actual final screen renders score and retry controls at stage 12");
            yield return Capture("05_FinalResult");
            int seed=view.Run.seed;view.Restart(true);Check(view.Run.seed==seed&&!view.Run.Started&&view.Run.events.Count==0,"Same-opening retry restores seed, opportunities and history");
            view.Restart(false);Check(view.Run.seed!=seed&&!view.Run.Started,"New run creates a different random seed");
            results.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/demo-smoke.txt",results);File.WriteAllLines(Folder+"/PlayModeChecks.txt",results);Destroy(this);
        }
    }
}
#endif
