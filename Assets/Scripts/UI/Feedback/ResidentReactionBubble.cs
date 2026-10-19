using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class ResidentReactionBubble : MonoBehaviour
    {
        public ResidentsView owner;
        public UIFeedbackProfile profile;
        public RectTransform paper;
        public CanvasGroup group;
        public Text label;
        bool showing;
        float age;
        public bool IsShowing=>showing;
        public string RawText { get; private set; }
        void Awake() { EnsureGroup(); }
        void EnsureGroup()
        {
            if(!paper)return;
            if(!group)group=paper.GetComponent<CanvasGroup>();
            if(!group)group=paper.gameObject.AddComponent<CanvasGroup>();
        }
        public void Show(string text,ResidentReaction reaction)
        {
            EnsureGroup();
            age=0;showing=true;paper.gameObject.SetActive(true);label.gameObject.SetActive(true);RawText=text;label.text=text.Replace("，","，\n").Replace("……","……\n");
            label.color=reaction==ResidentReaction.Overloaded?new Color(.58f,.19f,.13f):reaction==ResidentReaction.Happy?new Color(.14f,.40f,.26f):new Color(.13f,.10f,.07f);
            bool motion=FrontendServices.Instance.settings.motion;group.alpha=motion?0:1;paper.localScale=Vector3.one*(motion?.96f:1);
        }
        void Update()
        {
            if(!showing)return;
            if(owner.overlay.gameObject.activeSelf) { group.alpha=0;return; }
            if(owner.EffectsPaused)return;
            age+=Time.unscaledDeltaTime;
            float duration=profile?profile.reactionSeconds:4.5f,enter=profile?profile.reactionEnter:.14f,exit=profile?profile.reactionExit:.25f;
            if(age>=duration) { Clear();return; }
            bool motion=FrontendServices.Instance.settings.motion;
            group.alpha=motion?Mathf.Min(Mathf.Clamp01(age/enter),Mathf.Clamp01((duration-age)/exit)):1;
            paper.localScale=Vector3.one*(motion?Mathf.Lerp(.96f,1,Mathf.SmoothStep(0,1,age/enter)):1);
        }
        public void Clear() { showing=false;if(paper) { paper.gameObject.SetActive(false);paper.localScale=Vector3.one; }if(label)label.gameObject.SetActive(false); }
        void OnDisable() { Clear(); }
    }
}
