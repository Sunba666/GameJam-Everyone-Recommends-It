using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EveryoneRecommends.Frontend
{
    // Animate existing label/icon children. The Button and its raycast rectangle never move.
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler,ISubmitHandler
    {
        public UIFeedbackProfile profile;
        public RectTransform[] visuals;
        Vector3[] scales;
        Button button;
        bool hovering,pressed,selected;
        float amount=1,submitUntil;
        public float VisualScale=>amount;
        void Awake() { button=GetComponent<Button>();scales=new Vector3[visuals.Length];for(int i=0;i<visuals.Length;i++)scales[i]=visuals[i].localScale; }
        bool CanAnimate=>button&&button.IsInteractable()&&FrontendServices.Instance&&FrontendServices.Instance.settings.motion;
        public void OnPointerEnter(PointerEventData e) { hovering=true; }
        public void OnPointerExit(PointerEventData e) { hovering=false;pressed=false; }
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)pressed=true; }
        public void OnPointerUp(PointerEventData e) { pressed=false; }
        public void OnSelect(BaseEventData e) { selected=true; }
        public void OnDeselect(BaseEventData e) { selected=false; }
        public void OnSubmit(BaseEventData e) { submitUntil=Time.unscaledTime+.10f; }
        void LateUpdate()
        {
            if(scales==null)return;
            float desired=CanAnimate?pressed||Time.unscaledTime<submitUntil?(profile?profile.pressedScale:.94f):hovering||selected?(profile?profile.hoverScale:1.055f):1:1;
            amount=CanAnimate?Mathf.Lerp(amount,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*4/(profile?profile.buttonResponse:.10f))):1;
            if(Mathf.Abs(amount-desired)<.001f)amount=desired;
            for(int i=0;i<visuals.Length;i++)if(visuals[i])visuals[i].localScale=scales[i]*amount;
        }
        void OnDisable()
        {
            hovering=pressed=selected=false;submitUntil=0;amount=1;
            if(scales!=null)for(int i=0;i<visuals.Length;i++)if(visuals[i])visuals[i].localScale=scales[i];
        }
    }
}
