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
        public AudioClip residentsMusic;
        public AudioClip[] recommendationSounds;
        public CharacterSceneWipe transition;
        public Preferences settings = new Preferences();
        public AudioSource MusicSource { get; private set; }
        public AudioSource SfxSource { get; private set; }
        public bool IsTransitioning => transition && transition.IsBusy;
        float _lastPreview;
        float _musicGain;
        float _outgoingGain;
        AudioSource spareMusic;
        Coroutine musicFade;
        public int RecommendationSoundsPlayed { get; private set; }
        public int LastSoundTopic { get; private set; }=-1;
        public int SettingsSaveCount { get; private set; }
        public ResidentRun CurrentRun { get; private set; }
        public EndingAchievements Achievements { get; private set; }
        public EndingId CurrentEnding { get; private set; }=EndingId.None;
        public bool CurrentEndingWasNew { get; private set; }
        ResidentRun awardedRun;
        void ClearEnding() { awardedRun=null;CurrentEnding=EndingId.None;CurrentEndingWasNew=false; }
        public void RequestNewRun() { CurrentRun = null;ClearEnding(); }
        public EndingId CollectFinishedEnding()
        {
            if(CurrentRun==null||!CurrentRun.Finished)return EndingId.None;
            if(awardedRun==CurrentRun)return CurrentEnding;
            CurrentEnding=CurrentRun.Ending();CurrentEndingWasNew=Achievements.Unlock(CurrentEnding);awardedRun=CurrentRun;
            return CurrentEnding;
        }
#if UNITY_EDITOR
        public EndingAchievements ReplaceAchievementStoreForTest(EndingAchievements replacement)
        {
            var previous=Achievements;Achievements=replacement;ClearEnding();return previous;
        }
#endif
        public ResidentRun EnsureRun(int count, float duration, int stages, SimulationParameters rules=null)
        {
            if (CurrentRun == null || CurrentRun.residents.Length != count)
                CurrentRun = new ResidentRun(count, duration, stages, Guid.NewGuid().GetHashCode(),rules);
            return CurrentRun;
        }
        public ResidentRun RetryRun(int count,float duration,int stages,int seed,SimulationParameters rules)
        {
            ClearEnding();CurrentRun=new ResidentRun(count,duration,stages,seed,rules);return CurrentRun;
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Achievements=new EndingAchievements();
            try { settings = JsonUtility.FromJson<Preferences>(PlayerPrefs.GetString(PreferenceKey, JsonUtility.ToJson(new Preferences()))) ?? new Preferences(); }
            catch { settings = new Preferences(); }
            MusicSource = gameObject.AddComponent<AudioSource>();
            MusicSource.loop = true; MusicSource.clip = musicClip; MusicSource.playOnAwake = false;
            spareMusic=gameObject.AddComponent<AudioSource>();spareMusic.loop=true;spareMusic.playOnAwake=false;
            SfxSource = gameObject.AddComponent<AudioSource>(); SfxSource.playOnAwake = false;
            Apply(false);
            // Keep a windowed first launch inside the desktop, including scaled Windows displays.
            if(!Application.isEditor&&!settings.fullscreen&&(Screen.width>1280||Screen.height>720))
                Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            SelectSceneMusic(SceneManager.GetActiveScene().name);
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyCursor();
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ApplyCursor();SelectSceneMusic(scene.name); }
        void SelectSceneMusic(string scene)
        {
            AudioClip clip=scene=="Residents"&&residentsMusic?residentsMusic:musicClip;
            if(!clip||MusicSource.clip==clip&&MusicSource.isPlaying)return;
            if(musicFade!=null)StopCoroutine(musicFade);
            float oldGain=_musicGain;var outgoing=MusicSource;MusicSource=spareMusic;spareMusic=outgoing;
            _musicGain=0;_outgoingGain=outgoing.isPlaying?oldGain:0;
            MusicSource.clip=clip;MusicSource.volume=0;MusicSource.Play();musicFade=StartCoroutine(FadeMusic());
        }
        IEnumerator FadeMusic()
        {
            float time=0,oldGain=_outgoingGain;
            while(time<.75f)
            {
                time+=Time.unscaledDeltaTime;_musicGain=Mathf.SmoothStep(0,1,time/.75f);
                _outgoingGain=oldGain*(1-_musicGain);ApplyMusicVolume();yield return null;
            }
            _musicGain=1;_outgoingGain=0;spareMusic.Stop();ApplyMusicVolume();musicFade=null;
        }
        void ApplyMusicVolume()
        {
            if(MusicSource)MusicSource.volume=settings.music*_musicGain*(MusicSource.clip==residentsMusic ? .72f : 1);
            if(spareMusic)spareMusic.volume=settings.music*_outgoingGain*(spareMusic.clip==residentsMusic ? .72f : 1);
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
            ApplyMusicVolume();
            if (SfxSource) SfxSource.volume = settings.sfx;
            // Editor Game view cannot change the desktop window's fullscreen mode.
            if (!Application.isEditor && Screen.fullScreen != settings.fullscreen) Screen.fullScreen = settings.fullscreen;
            if (save) SaveSettings();
            SettingsChanged?.Invoke();
        }
        public void SaveSettings()
        {
            string data=JsonUtility.ToJson(settings);if(PlayerPrefs.GetString(PreferenceKey,"")==data)return;
            PlayerPrefs.SetString(PreferenceKey,data);PlayerPrefs.Save();SettingsSaveCount++;
        }
        void OnApplicationQuit() { if(Instance==this)SaveSettings(); }
        public void ResetDefaults() { settings = new Preferences(); Apply(); }
        public void Click() { if (SfxSource && clickClip) SfxSource.PlayOneShot(clickClip); }
        public void PlayRecommendation(int topic,bool neighbour)
        {
            if(!SfxSource||recommendationSounds==null||topic<0||topic>=recommendationSounds.Length||!recommendationSounds[topic])return;
            SfxSource.PlayOneShot(recommendationSounds[topic],neighbour ? .30f : .44f);
            LastSoundTopic=topic;RecommendationSoundsPlayed++;
        }
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
