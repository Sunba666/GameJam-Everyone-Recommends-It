using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    // One instance survives scene changes. All menu settings apply immediately.
    [DefaultExecutionOrder(-1000)]
    public sealed class FrontendServices : MonoBehaviour
    {
        [Serializable] public sealed class Preferences
        {
            public float master = .8f, music = .65f, sfx = .8f;
            public bool fullscreen, motion = true;
        }
        public const string PreferenceKey = "EveryoneRecommends.Frontend.Settings.v1";
        public static FrontendServices Instance { get; private set; }
        public static event Action SettingsChanged;
        public Texture2D cursorTexture;
        public Vector2 cursorHotspot = new Vector2(10, 5);
        public AudioClip musicClip, clickClip;
        public CharacterSceneWipe transition;
        public Preferences settings = new Preferences();
        public AudioSource MusicSource { get; private set; }
        public AudioSource SfxSource { get; private set; }
        public bool IsTransitioning => transition && transition.IsBusy;
        float _lastPreview;
        float _musicGain;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try { settings = JsonUtility.FromJson<Preferences>(PlayerPrefs.GetString(PreferenceKey, JsonUtility.ToJson(new Preferences()))) ?? new Preferences(); }
            catch { settings = new Preferences(); }
            MusicSource = gameObject.AddComponent<AudioSource>();
            MusicSource.loop = true; MusicSource.clip = musicClip; MusicSource.playOnAwake = false;
            SfxSource = gameObject.AddComponent<AudioSource>(); SfxSource.playOnAwake = false;
            Apply(false);
            if (musicClip) { MusicSource.Play(); StartCoroutine(FadeInMusic()); }
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyCursor();
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ApplyCursor(); }
        IEnumerator FadeInMusic()
        {
            float time=0;
            while(time<.75f)
            {
                time+=Time.unscaledDeltaTime;_musicGain=Mathf.SmoothStep(0,1,time/.75f);
                MusicSource.volume=settings.music*_musicGain;yield return null;
            }
            _musicGain=1;MusicSource.volume=settings.music;
        }
        void OnApplicationFocus(bool focus) { if (focus && Instance == this) ApplyCursor(); }
        public void ApplyCursor()
        {
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (cursorTexture) Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
        }
        public void Apply(bool save = true)
        {
            settings.master = Mathf.Clamp01(settings.master); settings.music = Mathf.Clamp01(settings.music); settings.sfx = Mathf.Clamp01(settings.sfx);
            AudioListener.volume = settings.master;
            if (MusicSource) MusicSource.volume = settings.music * _musicGain;
            if (SfxSource) SfxSource.volume = settings.sfx;
            // Editor Game view cannot change the desktop window's fullscreen mode.
            if (!Application.isEditor && Screen.fullScreen != settings.fullscreen) Screen.fullScreen = settings.fullscreen;
            if (save) { PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(settings)); PlayerPrefs.Save(); }
            SettingsChanged?.Invoke();
        }
        public void ResetDefaults() { settings = new Preferences(); Apply(); }
        public void Click() { if (SfxSource && clickClip) SfxSource.PlayOneShot(clickClip); }
        public void PreviewSound()
        {
            if (Time.unscaledTime - _lastPreview < .1f) return;
            _lastPreview = Time.unscaledTime; Click();
        }
        public void LoadScene(string name) { if (!IsTransitioning) transition.Begin(name); }
        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            AudioListener.volume = 1; Instance = null; SettingsChanged = null;
        }
    }
}
