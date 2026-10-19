#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // Presentation boundary fixtures use copied states; no recommendation or clock calls.
    public sealed class ResidentMotionAdditionsSmokeCheck : MonoBehaviour
    {
        const string Folder="Builds/Verification/ResidentMotionAdditions";
        const string Root="Assets/EveryoneRecommends/ResidentMotion/Variants";
        static readonly string[] Ids={"FoodHappy","GameFocused","NewsUnfold"};
        readonly List<string> checks=new List<string>();
        ResidentsView view;
        ResidentMotion driver;
        FrontendServices service;
        ResidentsView originalOwner;
        bool captured,originalMotion,originalEnabled,originalCardVisible;
        int originalCardResident;
        RuntimeAnimatorController[] originalAlternatives;
        Sprite[] originalStills;

        [Serializable] sealed class RunRecord
        {
            public ResidentState[] residents;
            public RunEvent[] events;
            public int stage,target,available;
            public float elapsed;
            public bool started,ended,finished,tutorial;
        }
        static string Signature(ResidentRun run)=>JsonUtility.ToJson(new RunRecord {
            residents=run.Snapshot(),events=run.events.ToArray(),stage=run.Stage,target=run.Target,available=run.Available,
            elapsed=run.Elapsed,started=run.Started,ended=run.StageEnded,finished=run.Finished,tutorial=run.TutorialDone });
        static string Geometry(ResidentsView owner)=>string.Join("\n",owner.residents.Select(image=>
            image.rectTransform.anchoredPosition.ToString("R")+";"+image.rectTransform.sizeDelta.ToString("R")+";"+
            image.rectTransform.localScale.ToString("R")+";"+image.rectTransform.localEulerAngles.ToString("R")));
        void Check(bool success,string message)
        {
            checks.Add((success?"PASS ":"FAIL ")+message);Flush();
            if(!success)throw new InvalidOperationException(message);
        }
        void Flush()
        {
            File.WriteAllLines(Folder+"/Checks.txt",checks);File.WriteAllLines("Library/resident-motion-additions-smoke.txt",checks);
        }
        void Restore()
        {
            if(!captured)return;captured=false;
            if(driver) { driver.owner=originalOwner;driver.engagedControllers=originalAlternatives;driver.engagedStills=originalStills; }
            if(service) { service.settings.motion=originalMotion;service.Apply(false); }
            if(view)
            {
                view.statusCard.Hide();view.RenderStates(view.Run.residents);
                if(originalCardVisible&&originalCardResident>=0)view.statusCard.Show(originalCardResident);
                view.enabled=originalEnabled;
            }
        }
        void OnDestroy()=>Restore();
        ResidentState[] Fixture(int topic)
        {
            var states=view.Run.Snapshot();var state=states[4];
            state.preferredTopic=topic;state.lastTopic=topic;state.reactionKind=ResidentReaction.Happy;
            state.interest[topic]=.8f;state.saturation[topic]=0;return states;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);service=FrontendServices.Instance;view=FindObjectOfType<ResidentsView>();
            Check(service&&view&&view.Run!=null,"Residents scene and live service are available");
            Check(view.motions!=null&&view.motions.Length==9&&view.motions.All(value=>value),"All nine existing residents retain motion drivers");
            driver=view.motions[4];originalOwner=driver.owner;originalAlternatives=driver.engagedControllers;originalStills=driver.engagedStills;
            originalMotion=service.settings.motion;originalEnabled=view.enabled;originalCardVisible=view.statusCard.gameObject.activeSelf;originalCardResident=view.statusCard.SelectedResident;
            string before=Signature(view.Run),geometry=Geometry(view),savedSettings=PlayerPrefs.GetString(FrontendServices.PreferenceKey,"");
            bool savedAchievement=PlayerPrefs.HasKey(EndingAchievements.SaveKey);int achievement=PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1),saves=service.SettingsSaveCount;
            captured=true;view.enabled=false;view.statusCard.Hide();
            try
            {
                Check(view.motions.All(value=>value.engagedControllers!=null&&value.engagedControllers.Length==5&&value.engagedStills!=null&&value.engagedStills.Length==5),"Five-slot optional controller and static-pose arrays are wired on all residents");
                for(int action=0;action<3;action++)
                {
                    string id=Ids[action];var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/"+id+"/"+id+"_UI.anim");
                    Check(clip,"Completed "+id+" clip is imported");
                    var bindings=AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    Check(bindings.Length==1&&bindings[0].path==""&&bindings[0].type==typeof(Image)&&bindings[0].propertyName=="m_Sprite"&&
                        AnimationUtility.GetCurveBindings(clip).Length==0,"The "+id+" clip changes only Image.sprite, never actor geometry");
                    var curve=AnimationUtility.GetObjectReferenceCurve(clip,bindings[0]);
                    var firstFrame=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/"+id+"/Frames/"+id+"_0000.png");
                    var lastFrame=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/"+id+"/Frames/"+id+"_0120.png");
                    bool loop=AnimationUtility.GetAnimationClipSettings(clip).loopTime;
                    string detail=" [keys="+curve.Length+", rate="+clip.frameRate.ToString("R")+", length="+clip.length.ToString("R")+", loop="+loop+"]";
                    Check(curve.Length==121&&clip.frameRate==24&&Math.Abs(clip.length-121f/24)<.002f&&
                        curve[0].value==firstFrame&&curve[120].value==lastFrame&&Math.Abs(curve[120].time-120f/24)<.0001f&&loop,
                        "The "+id+" clip preserves every source frame in a full native-duration loop"+detail);
                    Check(curve.All(key=>key.value is Sprite)&&curve.Select(key=>(Sprite)key.value).All(sprite=>sprite.texture.width<=256&&sprite.texture.height<=256&&
                        sprite.texture.filterMode==FilterMode.Bilinear&&sprite.texture.wrapMode==TextureWrapMode.Clamp),"The "+id+" frames retain smooth bounded texture imports");
                }
                service.settings.motion=true;service.Apply(false);
                // Detached presentation owner lets the Animator advance without advancing
                // the formal game, including when the actual run is still in preparation.
                driver.owner=null;
                for(int topic=1;topic<=3;topic++)
                {
                    var states=Fixture(topic);view.RenderStates(states);yield return null;
                    Check(driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.engagedControllers[topic],"Copied true-state fixture selects "+Ids[topic-1]);
                    Sprite first=driver.target.sprite;yield return new WaitForSecondsRealtime(.19f);
                    Check(driver.target.sprite&&driver.target.sprite!=first&&AssetDatabase.GetAssetPath(driver.target.sprite).StartsWith(Root+"/"+Ids[topic-1]+"/Frames/"),"The "+Ids[topic-1]+" sprite sequence advances in Play mode");
                    float clock=driver.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    for(int repeat=0;repeat<20;repeat++)view.RenderStates(states);
                    Check(Math.Abs(driver.animator.GetCurrentAnimatorStateInfo(0).normalizedTime-clock)<.0001f,"Repeated "+Ids[topic-1]+" state refreshes do not reset the playing controller");
                    states[4].saturation[topic]=states[4].aversionThresholds[topic];view.RenderStates(states);
                    Check(!driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.controllers[topic],"At the "+Ids[topic-1]+" aversion boundary the base activity remains available");
                }
                var food=Fixture(1);food[4].lastTopic=4;view.RenderStates(food);
                Check(!driver.UsingEngagedMotion,"Happy reaction to a different topic never claims happy eating");
                food=Fixture(1);food[4].reactionKind=ResidentReaction.Curious;view.RenderStates(food);
                Check(!driver.UsingEngagedMotion,"Curiosity alone does not trigger happy eating");
                for(int topic=2;topic<=3;topic++)
                {
                    var states=Fixture(topic);states[4].interest[topic]=.49f;view.RenderStates(states);
                    Check(!driver.UsingEngagedMotion,"Low interest does not trigger "+Ids[topic-1]);
                    states=Fixture(topic);states[4].preferredTopic=0;driver.SetTopic(topic,view.activities[topic==2?3:1],4,states[4]);
                    Check(!driver.UsingEngagedMotion,"A mismatched displayed preference does not trigger "+Ids[topic-1]);
                }
                for(int topic=2;topic<=3;topic++)
                {
                    var states=Fixture(topic);view.RenderStates(states);yield return null;
                    states[4].saturation[topic]=states[4].aversionThresholds[topic];
                    yield return null;yield return null;
                    Check(!driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.controllers[topic],"The "+Ids[topic-1]+" visual notices copied pressure reaching its threshold without RenderStates or a new event");
                    states[4].saturation[topic]=states[4].aversionThresholds[topic]-.01f;
                    yield return null;yield return null;
                    Check(driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.engagedControllers[topic],"The "+Ids[topic-1]+" visual resumes as copied pressure recovers without RenderStates or a new event");
                    float clock=driver.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    yield return new WaitForSecondsRealtime(.19f);
                    Check(driver.animator.GetCurrentAnimatorStateInfo(0).normalizedTime>clock,"Unchanged eligible "+Ids[topic-1]+" state keeps advancing instead of resetting each Update");
                }
                food=Fixture(1);food[4].reactionKind=ResidentReaction.Overloaded;food[4].saturation[1]=food[4].aversionThresholds[1];view.RenderStates(food);
                food[4].saturation[1]=0;yield return null;yield return null;
                Check(!driver.UsingEngagedMotion,"Recovered pressure after an actual overloaded-food reaction never invents a Happy reaction");
                food=Fixture(1);driver.engagedControllers=(RuntimeAnimatorController[])originalAlternatives.Clone();driver.engagedControllers[1]=null;view.RenderStates(food);
                Check(!driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.controllers[1],"A missing optional controller falls back to the original action");
                driver.engagedControllers=originalAlternatives;driver.engagedStills=(Sprite[])originalStills.Clone();driver.engagedStills[1]=null;view.RenderStates(food);
                Check(!driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.controllers[1],"A missing optional static pose also falls back to the original action");
                driver.engagedStills=originalStills;
                driver.SetTopic(-1,view.activities[0],4,food[4]);Check(!driver.animator.enabled&&driver.target.sprite==view.activities[0],"A negative topic safely disables animation and keeps the supplied still");
                driver.SetTopic(5,view.activities[0],4,food[4]);Check(!driver.animator.enabled&&driver.target.sprite==view.activities[0],"An out-of-range topic safely keeps the supplied still");
                driver.SetTopic(1,view.activities[4],4);Check(!driver.UsingEngagedMotion&&driver.animator.runtimeAnimatorController==driver.controllers[1],"The original three-argument API retains the original activity without a supplied state");

                view.RenderStates(food);driver.owner=originalOwner;view.statusCard.Show(4);yield return null;
                float pausedClock=driver.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;yield return new WaitForSecondsRealtime(.19f);
                Check(view.PausedForWorld&&driver.animator.speed==0&&Math.Abs(driver.animator.GetCurrentAnimatorStateInfo(0).normalizedTime-pausedClock)<.0001f,"The new activity respects observation/world pause without advancing its animation clock");
                Check(view.statusCard.portrait.sprite==driver.target.sprite,"The open status card copies the actual new animation frame");
                service.settings.motion=false;service.Apply(false);yield return null;
                Check(!driver.animator.enabled&&driver.target.sprite==driver.engagedStills[1]&&view.statusCard.portrait.sprite==driver.target.sprite,"Reduced motion uses the fitted new still in both scene and status card");
                Check(Signature(view.Run)==before,"All variant and boundary fixtures leave formal residents, events, time and opportunities unchanged");
                Check(Geometry(view)==geometry,"All nine actor positions, sizes, scales and rotations remain unchanged");
            }
            finally { Restore(); }
            Check(Signature(view.Run)==before,"Restoring the live display does not modify the game run");
            Check(service.settings.motion==originalMotion&&service.SettingsSaveCount==saves&&PlayerPrefs.GetString(FrontendServices.PreferenceKey,"")==savedSettings&&
                PlayerPrefs.HasKey(EndingAchievements.SaveKey)==savedAchievement&&PlayerPrefs.GetInt(EndingAchievements.SaveKey,-1)==achievement,"Original motion preferences and persistent settings/achievements remain unchanged; no settings save occurs");
            checks.Add("COMPLETE "+DateTime.Now.ToString("O"));Flush();Destroy(this);
        }
    }
}
#endif
