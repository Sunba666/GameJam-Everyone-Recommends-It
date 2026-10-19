using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // The dimmer stays over the whole screen; only the window and its controls travel.
    public sealed class PopupSlide : MonoBehaviour
    {
        public RectTransform panel;
        public Image scrim;
        [Min(.1f)] public float duration = .52f;
        Vector2 _rest;
        Color _scrimColor;
        CanvasGroup _controls;
        bool _initialized;
        public bool IsAnimating { get; private set; }

        void Initialize()
        {
            if (_initialized || !panel) return;
            _rest = panel.anchoredPosition;
            _controls = panel.GetComponent<CanvasGroup>();
            if (!_controls) _controls = panel.gameObject.AddComponent<CanvasGroup>();
            if (scrim) _scrimColor = scrim.color;
            _initialized = true;
        }
        void OnEnable()
        {
            Initialize();
            FrontendServices.SettingsChanged += OnSettingsChanged;
            if (!_initialized) return;
            if (!FrontendServices.Instance || !FrontendServices.Instance.settings.motion) { Finish(); return; }
            StartCoroutine(Enter());
        }
        IEnumerator Enter()
        {
            Canvas.ForceUpdateCanvases();
            var root = transform as RectTransform;
            float height = root ? root.rect.height : 941;
            float offset = height * .5f + panel.rect.height * (1 - panel.pivot.y) + Mathf.Abs(_rest.y) + 40;
            IsAnimating = true; _controls.interactable = false;
            float time = 0;
            while (time < duration)
            {
                float t = Mathf.Clamp01(time / duration);
                // Quintic ease-out: quick launch, soft landing, no overshoot.
                panel.anchoredPosition = _rest + Vector2.up * (offset * Mathf.Pow(1 - t, 5));
                if (scrim) { var c = _scrimColor; c.a *= Mathf.Clamp01(time / .18f); scrim.color = c; }
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            Finish();
        }
        void OnSettingsChanged()
        {
            if (FrontendServices.Instance && !FrontendServices.Instance.settings.motion) { StopAllCoroutines(); Finish(); }
        }
        void Finish()
        {
            if (!_initialized) return;
            panel.anchoredPosition = _rest;
            if (scrim) scrim.color = _scrimColor;
            _controls.interactable = true; IsAnimating = false;
        }
        void OnDisable()
        {
            FrontendServices.SettingsChanged -= OnSettingsChanged;
            StopAllCoroutines(); Finish();
        }
    }
}
