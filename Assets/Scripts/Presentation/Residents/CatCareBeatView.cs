using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // One expressive resident, using existing separate poses and cat art. No simulation writes.
    public sealed class CatCareBeatView : MonoBehaviour
    {
        public ResidentsView owner;
        public int residentIndex=0;
        public RectTransform stage;
        public Image body,cat,swipedCard;
        public Text caption;
        public Sprite tabletPose,carePose;
        public Image originalCatProp;
        public Outline careOutline;
        public bool CareVisible { get; private set; }
        public bool IsPlaying { get; private set; }
        public float BeatTime { get; private set; }
        public bool IsSwiping=>CareVisible&&IsPlaying&&BeatTime<1.1f;
        RunEvent pending;
        bool eligible;
        float motionClock;
        ResidentState shownState;
        int shownPhase;
        public static bool Matches(RunEvent value,int index)
        {
            if(value==null||value.resident!=index||value.topic!=0||value.reaction!=ResidentReaction.Overloaded||value.after==null)return false;
            var r=value.after[index];return r.preferredTopic==0&&r.interest[0]>=.6f;
        }
        public void Queue(RunEvent value)
        {
            if(!Matches(value,residentIndex))return;
            pending=value;
        }
        public void Apply(ResidentState state,int phase)
        {
            shownState=state;shownPhase=phase;
            eligible=state.preferredTopic==0&&state.interest[0]>=.6f&&state.lastTopic==0&&state.reactionKind==ResidentReaction.Overloaded&&state.PressureRatio(0)>=1;
            // The physical cat is meaningful only after a real lifestyle object has appeared.
            bool hasCat=ResidentLifeStories.HasLifeObject(state,0,phase);
            CareVisible=eligible&&hasCat;
            if(!CareVisible&&!IsPlaying)SetVisible(false);
            else SetVisible(true);
        }
        void Update()
        {
            if(shownState!=null)Apply(shownState,shownPhase);
            if(pending!=null&&CareVisible&&owner.residents[residentIndex].gameObject.activeInHierarchy&&!owner.EffectsPaused&&!owner.overlay.gameObject.activeSelf)
            { pending=null;IsPlaying=true;BeatTime=0;SetVisible(true); }
            if(!CareVisible) { IsPlaying=false;pending=null;SetVisible(false);return; }
            bool motion=FrontendServices.Instance.settings.motion;
            bool paused=owner.EffectsPaused||owner.overlay.gameObject.activeSelf||owner.statusCard.gameObject.activeSelf;
            if(!paused) { if(IsPlaying)BeatTime+=Time.unscaledDeltaTime;if(motion)motionClock+=Time.unscaledDeltaTime; }
            if(!motion&&IsPlaying) { BeatTime=4.5f;IsPlaying=false; }
            bool swiping=IsPlaying&&BeatTime<1.1f;
            body.sprite=swiping?tabletPose:carePose;
            float t=Mathf.Clamp01(BeatTime/1.1f);
            body.rectTransform.localEulerAngles=new Vector3(0,0,swiping?0:motion?Mathf.Sin(motionClock*2.3f)*2:0);
            cat.gameObject.SetActive(!swiping);
            cat.rectTransform.anchoredPosition=new Vector2(66,-44+(motion?Mathf.Sin(motionClock*2.3f)*2:0));
            body.color=owner.residents[residentIndex].color;
            if(careOutline) { var original=owner.residents[residentIndex].GetComponent<Outline>();careOutline.enabled=original&&original.enabled;if(original) { careOutline.effectColor=original.effectColor;careOutline.effectDistance=original.effectDistance; } }
            swipedCard.gameObject.SetActive(swiping);swipedCard.rectTransform.anchoredPosition=new Vector2(36+t*92,-23+t*12);
            swipedCard.color=new Color(1,1,1,1-t);swipedCard.rectTransform.localEulerAngles=new Vector3(0,0,-t*22);
            caption.text=swiping?"这条先划走":"视频先不看，猫还要照顾";
            if(IsPlaying&&BeatTime>=4.5f)IsPlaying=false;
        }
        void SetVisible(bool visible)
        {
            if(visible&&!body.sprite)body.sprite=pending!=null?tabletPose:carePose;
            stage.gameObject.SetActive(visible);
            if(owner&&residentIndex<owner.residents.Length)owner.residents[residentIndex].enabled=!visible;
            if(originalCatProp)originalCatProp.enabled=!visible;
        }
        public void Clear() { pending=null;shownState=null;eligible=false;CareVisible=IsPlaying=false;BeatTime=motionClock=0;SetVisible(false); }
        void OnDisable() { if(stage)Clear(); }
    }
}
