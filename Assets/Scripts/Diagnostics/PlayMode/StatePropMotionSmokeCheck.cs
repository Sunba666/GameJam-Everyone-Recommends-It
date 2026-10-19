#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class StatePropMotionSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/StatePropMotion";
        const string Root="Assets/EveryoneRecommends/ResidentMotion/Props";
        static readonly string[] Ids={"CatSitting","CatLying","MusicNotes"};
        readonly List<string> checks=new List<string>();
        ResidentsView view;FrontendServices service;bool captured,originalMotion,originalEnabled,originalOverlay,originalCardVisible,originalFocus;
        int originalCardIndex,originalFloor;FieldInfo focusField;
        [Serializable] sealed class RunRecord
        {
            public ResidentState[] residents;public RunEvent[] events;public int stage,target,available;public float elapsed;
            public bool started,ended,finished,tutorial;
        }
        static string Signature(ResidentRun run)=>JsonUtility.ToJson(new RunRecord {
            residents=run.Snapshot(),events=run.events.ToArray(),stage=run.Stage,target=run.Target,available=run.Available,elapsed=run.Elapsed,
            started=run.Started,ended=run.StageEnded,finished=run.Finished,tutorial=run.TutorialDone });
        static string Geometry(ResidentsView owner)=>string.Join("\n",owner.residents.Select(image=>
            image.rectTransform.anchoredPosition.ToString("R")+";"+image.rectTransform.sizeDelta.ToString("R")+";"+image.rectTransform.localScale.ToString("R")+";"+image.rectTransform.localEulerAngles.ToString("R")));
        void Flush() { File.WriteAllLines(Folder+"/Checks.txt",checks);File.WriteAllLines("Library/state-prop-motion-smoke.txt",checks); }
        void Check(bool success,string label)
        { checks.Add((success?"PASS ":"FAIL ")+label);Flush();if(!success)throw new InvalidOperationException(label); }
        void Restore()
        {
            if(!captured)return;captured=false;
            if(service) { service.settings.motion=false;service.Apply(false); }
            if(view)
            {
                view.catCare.Clear();view.statusCard.Hide();view.SelectFloor(originalFloor);view.RenderStates(view.Run.residents);view.overlay.gameObject.SetActive(originalOverlay);
                if(originalCardVisible&&originalCardIndex>=0)view.statusCard.Show(originalCardIndex);
                if(focusField!=null)focusField.SetValue(view,originalFocus);view.enabled=originalEnabled;
            }
            if(service) { service.settings.motion=originalMotion;service.Apply(false); }
        }
        void OnDestroy()=>Restore();
        IEnumerator Capture(string label)
        { yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+label+".png");yield return new WaitForEndOfFrame(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;view=FindObjectOfType<ResidentsView>();
            Check(service&&view&&view.Run!=null&&view.statePropMotion,"The live scene contains activity-companion presentation");
            var props=view.statePropMotion;originalMotion=service.settings.motion;originalEnabled=view.enabled;originalOverlay=view.overlay.gameObject.activeSelf;
            originalCardVisible=view.statusCard.gameObject.activeSelf;originalCardIndex=view.statusCard.SelectedResident;
            originalFloor=view.SelectedFloor;
            focusField=typeof(ResidentsView).GetField("focused",BindingFlags.Instance|BindingFlags.NonPublic);originalFocus=focusField!=null&&(bool)focusField.GetValue(view);
            string formal=Signature(view.Run),geometry=Geometry(view),settings=PlayerPrefs.GetString(FrontendServices.PreferenceKey,"");
            int saves=service.SettingsSaveCount,achievement=PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1);bool hasAchievement=PlayerPrefs.HasKey(EndingAchievements.SaveKey);
            captured=true;view.enabled=false;view.statusCard.Hide();view.overlay.gameObject.SetActive(false);view.SendMessage("OnApplicationFocus",true);
            try
            {
                Check(props.companions.Length==9&&props.companionAnimators.Length==9&&props.careAnimator,"Nine activity companions and the separate care cat are wired");
                for(int action=0;action<3;action++)
                {
                    string id=Ids[action];var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/"+id+"/"+id+"_UI.anim");Check(clip,"The "+id+" clip exists");
                    var bindings=AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    Check(bindings.Length==1&&bindings[0].type==typeof(Image)&&bindings[0].path==""&&bindings[0].propertyName=="m_Sprite"&&AnimationUtility.GetCurveBindings(clip).Length==0,"The "+id+" clip only changes the sprite, not geometry");
                    var curve=AnimationUtility.GetObjectReferenceCurve(clip,bindings[0]);
                    Check(curve.Length==121&&clip.frameRate==24&&Math.Abs(clip.length-121/24f)<.002f&&AnimationUtility.GetAnimationClipSettings(clip).loopTime,"The "+id+" loop retains native121frames/24fps/5.04seconds");
                    int maximum=action==2?128:256;
                    Check(curve.All(key=>key.value is Sprite)&&curve.Select(key=>(Sprite)key.value).All(sprite=>sprite.texture.width<=maximum&&sprite.texture.height<=maximum&&sprite.texture.filterMode==FilterMode.Bilinear),"The "+id+" textures fit their visual memory budget");
                }
                service.settings.motion=true;service.Apply(false);
                var states=view.Run.Snapshot();states[4].preferredTopic=0;states[4].propsUntil[0]=0;states[5].preferredTopic=4;states[5].propsUntil[4]=view.Run.Stage+2;
                for(int bottom=6;bottom<9;bottom++)states[bottom].preferredTopic=0;
                view.RenderStates(states);yield return null;yield return null;
                Check(props.companions[4].gameObject.activeSelf&&props.companionAnimators[4].runtimeAnimatorController==props.controllers[1],"A cat watcher has the lying cat even before a life-object record exists");
                Check(props.companions[5].gameObject.activeSelf&&props.companionAnimators[5].runtimeAnimatorController==props.controllers[2],"A music listener has the floating-note sequence");
                Check(!view.propImages[5*5+4].enabled&&view.propImages[5*5+4].gameObject.activeSelf,"The active music companion hides only the duplicate static graphic, preserving its life-object history");
                Check(props.companions.All(image=>!image.raycastTarget)&&!view.catCare.cat.raycastTarget&&view.residents.All(image=>image.GetComponent<Button>()),"All nine companions and the care cat leave original resident buttons clickable");
                Check(Enumerable.Range(0,9).All(index=>Enumerable.Range(0,9).Where(other=>other!=index).All(other=>
                    !view.statusCard.InArea(props.companions[index].rectTransform).Contains(view.statusCard.InArea(view.residents[other].rectTransform).center))),"Companion rectangles do not cover any neighboring resident's button center");
                if(originalFloor==0)Check(Enumerable.Range(6,3).All(index=>!view.statusCard.InArea(props.companions[index].rectTransform).Overlaps(view.statusCard.InArea(view.feedback.rectTransform))),"All three bottom-floor cat layers clear the bottom feedback strip");
                foreach(int index in new[]{4,5})
                {
                    var animator=props.companionAnimators[index];Sprite first=props.companions[index].sprite;
                    // An isolated manual sample verifies sequence playback without
                    // starting or changing the formal game merely for this visual test.
                    animator.speed=1;animator.Update(.19f);
                    Check(props.companions[index].sprite!=first,"The "+(index==4?"lying cat":"floating notes")+" sequence advances when its Animator is sampled");
                    float clock=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;for(int repeat=0;repeat<20;repeat++)view.RenderStates(states);
                    Check(Math.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-clock)<.0001f,"Repeated renders do not restart the "+(index==4?"cat":"notes")+" sequence");
                }
                states[4].preferredTopic=1;states[4].propsUntil[0]=view.Run.Stage+1;states[5].preferredTopic=2;view.RenderStates(states);yield return null;
                Check(!props.companions[4].gameObject.activeSelf&&!props.companions[5].gameObject.activeSelf,"Food and game activity do not keep cat/music activity companions");
                Check(view.propImages[4*5].gameObject.activeSelf&&view.propImages[4*5].enabled,"An old cat object remains visible when its resident has changed activity");
                states[4].preferredTopic=4;view.RenderStates(states);yield return null;
                Check(props.companionAnimators[4].runtimeAnimatorController==props.controllers[2]&&props.companions[4].gameObject.activeSelf,"Changing the displayed activity changes its matching companion");
                int historical=view.Run.residents[5].preferredTopic==0?4:0;states[5].preferredTopic=historical;
                string historicalBefore=string.Join("\n",states.Select(state=>JsonUtility.ToJson(state)));
                view.RenderStates(states,Math.Max(1,view.Run.Stage-1));yield return null;
                Check(props.DisplayedTopic(5)==historical&&props.companionAnimators[5].runtimeAnimatorController==props.controllers[historical==0?1:2],"A historical snapshot drives the companion even when the formal resident has another activity");
                Check(historicalBefore==string.Join("\n",states.Select(state=>JsonUtility.ToJson(state)))&&Signature(view.Run)==formal,"Reading historical visuals changes neither the supplied snapshots nor the formal run");

                if(originalFloor==0&&!view.Run.Started)
                {
                    states[5].preferredTopic=4;view.RenderStates(states);yield return null;yield return null;
                    float phase=props.companionAnimators[5].GetCurrentAnimatorStateInfo(0).normalizedTime;
                    view.SelectFloor(2);yield return new WaitForSecondsRealtime(.55f);yield return null;
                    Check(view.PausedForWorld&&props.companionAnimators[5].enabled&&Math.Abs(props.companionAnimators[5].GetCurrentAnimatorStateInfo(0).normalizedTime-phase)<.0001f,"Entering a floor retains the visible music companion's phase through transient reparenting");
                    service.settings.motion=false;service.Apply(false);view.SelectFloor(originalFloor);yield return null;
                    service.settings.motion=true;service.Apply(false);yield return null;
                }
                service.settings.motion=false;service.Apply(false);view.SelectFloor(3);yield return null;
                Check(view.residents.Count(image=>image.gameObject.activeInHierarchy)==3,"The detail view hides six residents while retaining three on the selected floor");
                states[4].preferredTopic=4;view.RenderStates(states);service.settings.motion=true;service.Apply(false);yield return null;yield return null;
                Check(!props.companions[4].gameObject.activeInHierarchy&&!props.companionAnimators[4].enabled&&props.companions[4].sprite==props.stills[2],"Changing a hidden-floor resident defers Animator initialization and uses its matching still");
                states[4].preferredTopic=0;view.RenderStates(states);service.settings.motion=false;service.Apply(false);yield return null;
                Check(!props.companionAnimators[4].enabled&&props.companions[4].sprite==props.stills[1],"A hidden resident accepts a new cat activity and reduced-motion setting safely");
                states[4].preferredTopic=4;view.RenderStates(states);view.SelectFloor(2);yield return null;
                service.settings.motion=true;service.Apply(false);yield return null;yield return null;
                Check(props.companions[4].gameObject.activeInHierarchy&&props.companionAnimators[4].enabled&&props.companionAnimators[4].runtimeAnimatorController==props.controllers[2],"Revealing the resident initializes the latest matching animation without stale hidden-floor state");
                service.settings.motion=false;service.Apply(false);view.SelectFloor(originalFloor);yield return null;
                service.settings.motion=true;service.Apply(false);yield return null;

                states[4].preferredTopic=0;states[5].preferredTopic=4;view.RenderStates(states);view.statusCard.Show(4);yield return null;yield return null;
                float pausedCat=props.companionAnimators[4].GetCurrentAnimatorStateInfo(0).normalizedTime;
                float pausedNotes=props.companionAnimators[5].GetCurrentAnimatorStateInfo(0).normalizedTime;yield return new WaitForSecondsRealtime(.19f);
                Check(view.PausedForWorld&&props.companionAnimators[4].speed==0&&props.companionAnimators[5].speed==0&&
                    Math.Abs(props.companionAnimators[4].GetCurrentAnimatorStateInfo(0).normalizedTime-pausedCat)<.0001f&&Math.Abs(props.companionAnimators[5].GetCurrentAnimatorStateInfo(0).normalizedTime-pausedNotes)<.0001f,"Observation freezes both new companion animation clocks");
                view.statusCard.Hide();
                var care=states[0];care.preferredTopic=0;care.interest[0]=.9f;care.lastTopic=0;care.reactionKind=ResidentReaction.Overloaded;
                care.saturation[0]=care.aversionThresholds[0];care.propsUntil[0]=view.Run.Stage+1;view.RenderStates(states);
                var cue=new RunEvent{resident=0,topic=0,reaction=ResidentReaction.Overloaded,after=states};view.catCare.Queue(cue);
                yield return null;yield return null;
                Check(view.catCare.CareVisible&&view.catCare.IsSwiping&&!view.catCare.cat.gameObject.activeSelf&&!props.companions[0].gameObject.activeSelf,"The swipe beat shows no premature sitting or duplicate lying cat");
                yield return new WaitForSecondsRealtime(1.2f);yield return null;
                Check(view.catCare.CareVisible&&!view.catCare.IsSwiping&&view.catCare.cat.gameObject.activeInHierarchy&&props.careAnimator.runtimeAnimatorController==props.controllers[0],"After the real-state swipe cue the physical cat uses the sitting animation");
                Check(!props.companions[0].gameObject.activeSelf&&!view.propImages[0].enabled,"The care pose suppresses the lying cat and duplicate static cat");
                Sprite careFirst=view.catCare.cat.sprite;props.careAnimator.speed=1;props.careAnimator.Update(.19f);
                Check(view.catCare.cat.sprite!=careFirst,"The sitting cat's sequence advances under an isolated Animator sample");
                yield return Capture("ActivityCompanions_CatCareAndMusic");
                service.settings.motion=false;service.Apply(false);yield return null;
                Check(!props.companionAnimators[5].enabled&&props.companions[5].sprite==props.stills[2]&&AssetDatabase.GetAssetPath(props.stills[2]).EndsWith("MusicNotes_0030.png"),"Reduced motion uses the visible middle-frame notes still");
                Check(!props.careAnimator.enabled&&view.catCare.cat.sprite==props.stills[0],"Reduced motion uses a static sitting cat without losing the care pose");
                props.enabled=false;
                Check(view.propImages[4*5].enabled&&view.propImages[5*5+4].enabled&&props.companions.All(image=>!image.gameObject.activeSelf),"Disabling the companion layer restores original static graphics and leaves no permanently hidden life objects");
                props.enabled=true;yield return null;
                Check(Signature(view.Run)==formal,"Copied visual fixtures, manual samples and the swipe cue change no formal state, event, clock or opportunity");
                Check(Geometry(view)==geometry,"Existing nine resident positions, sizes, scales and rotations remain unchanged");
            }
            finally { Restore(); }
            Check(Signature(view.Run)==formal,"Restoring normal visuals leaves the formal run intact");
            Check(service.settings.motion==originalMotion&&service.SettingsSaveCount==saves&&PlayerPrefs.GetString(FrontendServices.PreferenceKey,"")==settings&&
                PlayerPrefs.HasKey(EndingAchievements.SaveKey)==hasAchievement&&PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1)==achievement,"Preferences and achievements remain unchanged and no settings save occurs");
            checks.Add("COMPLETE "+DateTime.Now.ToString("O"));Flush();Destroy(this);
        }
    }
}
#endif
