#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.Frontend
{
    public sealed class FrontendSmokeCheck : MonoBehaviour
    {
        readonly List<string> _results = new List<string>();
        FrontendServices.Preferences _original;
        const string Dir="Builds/Verification/MainMenu";
        void Check(bool pass,string name)
        {
            _results.Add((pass?"PASS ":"FAIL ")+name);
            File.WriteAllLines("Library/frontend-smoke.txt",_results);
        }
        void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir,name+".png"));
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);
            var service=FrontendServices.Instance;
            _original=JsonUtility.FromJson<FrontendServices.Preferences>(JsonUtility.ToJson(service.settings));
            yield return new WaitForSecondsRealtime(1.5f);
            var menu=FindObjectOfType<MenuView>();var entrance=FindObjectOfType<MenuEntrance>();
            Check(menu&&entrance&&entrance.Finished,"Menu and all entrance groups initialized");
            Check(menu.quit && menu.quit.gameObject.activeInHierarchy && menu.quit.interactable && menu.quit.GetComponentInChildren<UnityEngine.UI.Text>().text=="退出游戏","Exit button visible, wired and enabled (actual quit checked separately)");
            Check(entrance.groups.Length==5&&entrance.fades.Length==5,"Five independent character groups");
            var a=entrance.groups[0].anchoredPosition;
            yield return new WaitForSecondsRealtime(.35f);
            Check(!service.settings.motion || Vector2.Distance(a,entrance.groups[0].anchoredPosition)>.01f,"Idle horizontal motion");
            Capture("01_MainMenu");
            yield return new WaitForEndOfFrame();
            menu.howTo.onClick.Invoke();Check(menu.howToPanel.activeSelf,"How-to opens");
            menu.nextPage.onClick.Invoke();Check(menu.CurrentPage==1,"How-to next page");
            menu.previousPage.onClick.Invoke();Check(menu.CurrentPage==0&&!menu.previousPage.interactable,"How-to previous and bounds");
            menu.nextPage.onClick.Invoke();Capture("02_HowTo");yield return new WaitForSecondsRealtime(.15f);
            menu.howToClose.onClick.Invoke();Check(!menu.howToPanel.activeSelf,"How-to closes");
            menu.settings.onClick.Invoke();var settings=menu.settingsPanel.GetComponent<SettingsView>();
            Check(menu.settingsPanel.activeSelf,"Settings opens");
            var popup=menu.settingsPanel.GetComponent<PopupSlide>();
            Check(popup&&(!service.settings.motion||popup.IsAnimating),"Settings slides in from above");
            yield return new WaitForSecondsRealtime(.65f);
            Check(!popup.IsAnimating,"Settings ease-out reaches rest");
            Check(Mathf.Abs(settings.master.handleRect.rect.width-settings.master.handleRect.rect.height)<1,"Slider handle remains round");
            settings.master.value=.37f;settings.music.value=.42f;settings.sfx.value=.51f;
            Check(Mathf.Approximately(AudioListener.volume,.37f)&&Mathf.Approximately(service.MusicSource.volume,.42f)&&Mathf.Approximately(service.SfxSource.volume,.51f),"All three sliders affect actual audio sources");
            Check(settings.masterValue.text=="37%"&&settings.musicValue.text=="42%"&&settings.sfxValue.text=="51%","Slider percentage feedback");
            settings.motion.isOn=false;yield return null;
            a=entrance.groups[0].anchoredPosition;yield return new WaitForSecondsRealtime(.2f);
            Check(!entrance.animators[0].enabled&&a==entrance.groups[0].anchoredPosition,"Motion toggle stops idle animation");
            settings.fullscreen.isOn=!settings.fullscreen.isOn;
            Check(service.settings.fullscreen==settings.fullscreen.isOn,"Fullscreen preference wired (physical window switch requires player check)");
            Capture("03_Settings");yield return new WaitForSecondsRealtime(.15f);
            settings.reset.onClick.Invoke();
            Check(Mathf.Approximately(service.settings.master,.8f)&&Mathf.Approximately(service.settings.music,.65f)&&Mathf.Approximately(service.settings.sfx,.8f)&&service.settings.motion&&!service.settings.fullscreen,"Restore defaults applies and saves all fields");
            settings.back.onClick.Invoke();Check(!menu.settingsPanel.activeSelf,"Settings back closes");
            menu.settings.onClick.Invoke();settings.close.onClick.Invoke();Check(!menu.settingsPanel.activeSelf,"Settings cross closes");
            menu.achievements.onClick.Invoke();Check(menu.achievementsPanel.activeSelf,"Achievements opens");
            var achievementSlide=menu.achievementsPanel.GetComponent<PopupSlide>();
            Check(achievementSlide&&achievementSlide.IsAnimating,"Achievements slides in from above");
            yield return new WaitForSecondsRealtime(.65f);
            Check(!achievementSlide.IsAnimating,"Achievements settles in center");
            Capture("04_Achievements");yield return new WaitForSecondsRealtime(.15f);
            foreach(var b in menu.achievementsPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true))if(b.name=="Back")b.onClick.Invoke();
            Check(!menu.achievementsPanel.activeSelf,"Achievements back closes");
            menu.achievements.onClick.Invoke();foreach(var b in menu.achievementsPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true))if(b.name=="Close")b.onClick.Invoke();
            Check(!menu.achievementsPanel.activeSelf,"Achievements cross closes");
            string stored=PlayerPrefs.GetString(FrontendServices.PreferenceKey);
            Check(stored.Contains("master"),"Settings persisted");
            menu.start.onClick.Invoke();menu.start.onClick.Invoke();Check(service.transition.IsBusy,"Start transition and double-click guard");
            while(service.transition.Phase=="Exit"&&service.transition.PhaseProgress<.48f)yield return null;
            Capture("05_ExitMidpoint");
            while(service.transition.Phase=="Exit"&&service.transition.PhaseProgress<.92f)yield return null;
            bool corners=true;foreach(var p in new[]{new Vector2(1,1),new Vector2(Screen.width-1,1),new Vector2(1,Screen.height-1),new Vector2(Screen.width-1,Screen.height-1)})corners&=service.transition.CoveredByTails(p);
            Check(corners,"All four viewport corners covered before midpoint meeting");
            Capture("05b_ExitCorners");
            while(service.transition.Phase=="Exit")yield return null;
            bool covered=true;for(int x=0;x<=20;x++)for(int y=0;y<=12;y++)covered&=service.transition.CoveredByTails(new Vector2(Mathf.Lerp(1,Screen.width-1,x/20f),Mathf.Lerp(1,Screen.height-1,y/12f)));
            Check(covered,"273 viewport samples fully covered by moving tails at scene handoff");
            Capture("06_FullCover");
            while(service.transition.Phase=="Loading")yield return null;
            while(service.transition.Phase=="Enter"&&service.transition.PhaseProgress<.67f)yield return null;
            Capture("07_Enter");
            while(service.IsTransitioning)yield return null;
            Check(SceneManager.GetActiveScene().name=="Residents","Independent Residents scene loaded");
            Check(FindObjectsOfType<FrontendServices>().Length==1&&FindObjectsOfType<AudioListener>().Length==1&&FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length==1,"One global service, one listener and one EventSystem after load");
            Check(service.transition.CharactersPerSide>0,"Equal character lanes created for both directions");
            Check(Mathf.Abs(service.transition.LastExitDuration-1.5f)<.15f&&Mathf.Abs(service.transition.LastEnterDuration-1.2f)<.15f,"Exit 1.5s and enter 1.2s: "+service.transition.LastExitDuration.ToString("F3")+" / "+service.transition.LastEnterDuration.ToString("F3"));
            Check(PlayerPrefs.GetString(FrontendServices.PreferenceKey)==stored&&FrontendServices.Instance==service,"Settings and global cursor service survive scene change");
            Check(service.musicClip&&service.musicClip.channels==2&&service.musicClip.length>40,"New stereo instrumental loop assigned and retained");
            var building=FindObjectOfType<ResidentsView>();building.floors[1].onClick.Invoke();Check(building.SelectedFloor==2,"Building floor selection");
            building.topics[4].onClick.Invoke();Check(building.SelectedTopic==4&&building.selectionLabel.text.Contains("音乐"),"Building topic button has visible result");
            building.allFloors.onClick.Invoke();Check(building.SelectedFloor==0,"Return to whole-building view");
            Capture("08_Residents");yield return new WaitForSecondsRealtime(.2f);
            building.settingsButton.onClick.Invoke();Check(building.settingsPanel.activeSelf,"Settings also works in Residents scene");building.settingsPanel.GetComponent<SettingsView>().Hide();
            building.returnToMenu.onClick.Invoke();while(service.IsTransitioning)yield return null;
            yield return new WaitForSecondsRealtime(1.3f);
            Check(SceneManager.GetActiveScene().name=="MainMenu"&&FindObjectsOfType<FrontendServices>().Length==1,"Return transition to MainMenu without duplicate services");
            service.settings=_original;service.Apply();
            _results.Add("COMPLETE "+DateTime.Now.ToString("O"));File.WriteAllLines("Library/frontend-smoke.txt",_results);
            Destroy(this);
        }
        void OnDestroy() { if(_original!=null&&FrontendServices.Instance) {FrontendServices.Instance.settings=_original;FrontendServices.Instance.Apply();} }
    }
}
#endif
