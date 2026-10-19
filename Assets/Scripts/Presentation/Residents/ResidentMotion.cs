using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    [RequireComponent(typeof(Image),typeof(Animator))]
    public sealed class ResidentMotion : MonoBehaviour
    {
        public RuntimeAnimatorController[] controllers;
        // Optional visual variants. Base controllers and their serialized references stay intact.
        public RuntimeAnimatorController[] engagedControllers;
        public Sprite[] engagedStills;
        public ResidentsView owner;
        public Image target;
        public Animator animator;
        public int Topic { get; private set; }=-1;
        public bool UsingEngagedMotion { get; private set; }
        Sprite fallback,baseStill;
        ResidentState displayedState;
        int residentIndex;
        float offset;
        void Awake() { Initialize(); }
        void OnEnable() { FrontendServices.SettingsChanged+=Apply;Apply(); }
        void OnDisable() { FrontendServices.SettingsChanged-=Apply; }
        void Initialize()
        {
            if(!target)target=GetComponent<Image>();if(!animator)animator=GetComponent<Animator>();
            animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
        }
        public void SetTopic(int topic,Sprite still,int index,ResidentState state=null)
        {
            Initialize();residentIndex=index;offset=(residentIndex*.137f)%1;Topic=topic;baseStill=still;displayedState=state;
            Apply();
        }
        bool RefreshSelection()
        {
            // Live pressure can recover without an event. Historical state references
            // remain frozen snapshots; neither path reads or writes the formal run here.
            bool valid=Topic>=0&&Topic<5;
            var controller=valid&&controllers!=null&&Topic<controllers.Length?controllers[Topic]:null;
            UsingEngagedMotion=valid&&EngagedState(Topic,displayedState)&&engagedControllers!=null&&Topic<engagedControllers.Length&&engagedControllers[Topic]&&
                engagedStills!=null&&Topic<engagedStills.Length&&engagedStills[Topic];
            Sprite still=baseStill;
            if(UsingEngagedMotion) { controller=engagedControllers[Topic];still=engagedStills[Topic]; }
            bool changed=fallback!=still;fallback=still;
            if(animator.runtimeAnimatorController!=controller)
            {
                animator.enabled=false;animator.runtimeAnimatorController=controller;changed=true;
            }
            return changed;
        }
        static bool EngagedState(int topic,ResidentState state)
        {
            if(state==null||state.preferredTopic!=topic||state.interest==null||state.interest.Length<=topic||
                state.saturation==null||state.saturation.Length<=topic||state.aversionThresholds==null||state.aversionThresholds.Length<=topic)return false;
            if(state.PressureRatio(topic)>=1)return false;
            if(topic==1)return state.lastTopic==1&&state.reactionKind==ResidentReaction.Happy;
            return (topic==2||topic==3)&&state.interest[topic]>=.5f;
        }
        void Apply()
        {
            Initialize();RefreshSelection();
            bool enabledMotion=Topic>=0&&Topic<5&&FrontendServices.Instance&&FrontendServices.Instance.settings.motion&&animator.runtimeAnimatorController;
            if(enabledMotion&&!gameObject.activeInHierarchy) { animator.enabled=false;return; }
            if(enabledMotion&&!animator.enabled)
            {
                animator.speed=owner&&owner.PausedForWorld?0:1;
                animator.enabled=true;animator.Rebind();animator.Play(0,0,offset);animator.Update(0);
            }
            else if(!enabledMotion) { animator.enabled=false;target.sprite=fallback; }
        }
        void Update()
        {
            if(RefreshSelection())Apply();
            if(animator&&animator.enabled)animator.speed=owner&&owner.PausedForWorld?0:1;
        }
    }
}
