using System.Collections;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    public sealed class MenuEntrance : MonoBehaviour
    {
        public RectTransform[] groups;
        public CanvasGroup[] fades;
        public RectTransform[] circles;
        public CanvasGroup[] circleFades;
        public Animator[] animators;
        [Min(.1f)] public float entrySeconds = .65f;
        public float stagger = .1f, swayPixels = 5, swayDegrees = 1;
        Vector2[] _positions; Vector3[] _scales; float _elapsed;
        public bool Finished { get; private set; }
        void Awake()
        {
            _positions = new Vector2[groups.Length]; _scales = new Vector3[groups.Length];
            for (int i = 0; i < groups.Length; i++) { _positions[i] = groups[i].anchoredPosition; _scales[i] = groups[i].localScale; }
        }
        void OnEnable() { FrontendServices.SettingsChanged += ApplyMotion; }
        void OnDisable() { FrontendServices.SettingsChanged -= ApplyMotion; }
        IEnumerator Start()
        {
            for (int i = 0; i < groups.Length; i++) { groups[i].localScale = Vector3.zero; fades[i].alpha = 0; circleFades[i].alpha = 0; animators[i].enabled = false; }
            if (!FrontendServices.Instance.settings.motion) { Finish(); yield break; }
            float total = entrySeconds + stagger * (groups.Length - 1);
            while (_elapsed < total)
            {
                if (!FrontendServices.Instance.settings.motion) break;
                _elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < groups.Length; i++)
                {
                    float t = Mathf.Clamp01((_elapsed - i * stagger) / entrySeconds);
                    float b = t - 1; float scale = 1 + 2.3f * b * b * b + 1.3f * b * b;
                    groups[i].localScale = _scales[i] * Mathf.Max(.001f, scale);
                    fades[i].alpha = Mathf.Clamp01(t * 3); circles[i].localScale = Vector3.one * Mathf.SmoothStep(.1f, 1, t); circleFades[i].alpha = t * .2f;
                }
                yield return null;
            }
            Finish();
        }
        void Finish()
        {
            Finished = true;
            for (int i = 0; i < groups.Length; i++) { groups[i].localScale = _scales[i]; fades[i].alpha = 1; circles[i].localScale = Vector3.one; circleFades[i].alpha = .2f; }
            ApplyMotion();
        }
        void ApplyMotion()
        {
            if (!FrontendServices.Instance || !Finished) return;
            bool enabledMotion = FrontendServices.Instance.settings.motion;
            for (int i = 0; i < groups.Length; i++)
            {
                if (!enabledMotion) { if(animators[i].gameObject.activeInHierarchy) { animators[i].Rebind(); animators[i].Update(0); }groups[i].anchoredPosition = _positions[i]; groups[i].localRotation = Quaternion.identity; }
                animators[i].enabled = enabledMotion;
            }
        }
        void Update()
        {
            if (!Finished || !FrontendServices.Instance || !FrontendServices.Instance.settings.motion) return;
            for (int i = 0; i < groups.Length; i++)
            {
                float wave = Mathf.Sin(Time.unscaledTime * 1.35f + i * 1.2f);
                groups[i].anchoredPosition = _positions[i] + Vector2.right * (wave * swayPixels);
                groups[i].localRotation = Quaternion.Euler(0, 0, wave * swayDegrees);
            }
        }
    }
}
