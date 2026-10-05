using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class SettingsView : MonoBehaviour
    {
        public Slider master, music, sfx;
        public Toggle fullscreen, motion;
        public Text masterValue, musicValue, sfxValue;
        public Button close, back, reset;
        bool _subscribed;
        void Awake()
        {
            foreach(var slider in new[]{master,music,sfx})
            {
                slider.handleRect.sizeDelta=new Vector2(54,0);
                slider.handleRect.GetComponent<Image>().preserveAspect=true;
                // Let the complete track, including its transparent margins, accept clicks.
                var track=slider.transform.Find("Track");
                if(track)track.GetComponent<Image>().raycastTarget=true;
            }
            master.onValueChanged.AddListener(Master); music.onValueChanged.AddListener(Music); sfx.onValueChanged.AddListener(Sfx);
            fullscreen.onValueChanged.AddListener(Fullscreen); motion.onValueChanged.AddListener(Motion);
            close.onClick.AddListener(Hide); back.onClick.AddListener(Hide); reset.onClick.AddListener(ResetDefaults);
        }
        void OnEnable() { FrontendServices.SettingsChanged += Refresh; _subscribed = true; Refresh(); }
        void OnDisable() { if (_subscribed) FrontendServices.SettingsChanged -= Refresh; _subscribed = false; }
        void Refresh()
        {
            if (!FrontendServices.Instance) return;
            var p = FrontendServices.Instance.settings;
            master.SetValueWithoutNotify(p.master); music.SetValueWithoutNotify(p.music); sfx.SetValueWithoutNotify(p.sfx);
            fullscreen.SetIsOnWithoutNotify(p.fullscreen); motion.SetIsOnWithoutNotify(p.motion);
            masterValue.text = Mathf.RoundToInt(p.master * 100) + "%"; musicValue.text = Mathf.RoundToInt(p.music * 100) + "%"; sfxValue.text = Mathf.RoundToInt(p.sfx * 100) + "%";
        }
        void Master(float x) { FrontendServices.Instance.settings.master = x; FrontendServices.Instance.Apply(); FrontendServices.Instance.PreviewSound(); }
        void Music(float x) { FrontendServices.Instance.settings.music = x; FrontendServices.Instance.Apply(); }
        void Sfx(float x) { FrontendServices.Instance.settings.sfx = x; FrontendServices.Instance.Apply(); FrontendServices.Instance.PreviewSound(); }
        void Fullscreen(bool x) { FrontendServices.Instance.settings.fullscreen = x; FrontendServices.Instance.Apply(); }
        void Motion(bool x) { FrontendServices.Instance.settings.motion = x; FrontendServices.Instance.Apply(); }
        public void ResetDefaults() { FrontendServices.Instance.ResetDefaults(); }
        public void Hide() { gameObject.SetActive(false); }
    }
}
