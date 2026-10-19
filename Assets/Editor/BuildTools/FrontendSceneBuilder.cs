using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using EveryoneRecommends.Frontend;

namespace EveryoneRecommends.EditorTools
{
    [InitializeOnLoad]
    public static class FrontendSceneBuilder
    {
        const string Root = "Assets/EveryoneRecommends/";
        const string SettingsPath = Root + "SettingsUI/Prefabs/SettingsPanel.prefab";
        const string MainPath = Root + "AchievementsUI/MenuIntegration/MainMenu_WithAchievements.prefab";
        const string ServicesPath = "Assets/Prefabs/UI/FrontendServices.prefab";
        const string CommandFile = "Library/frontend-command.txt";
        static readonly Color Cream = new Color(.984f, .945f, .863f, 1);
        static Font Font;
        static double _nextPoll;
        static FrontendSceneBuilder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _nextPoll = EditorApplication.timeSinceStartup + .5;
            if(EditorApplication.isPlaying&&SessionState.GetBool("EveryoneRecommends.PendingInformationTrial",false)&&FrontendServices.Instance)
            { SessionState.EraseBool("EveryoneRecommends.PendingInformationTrial");FrontendServices.Instance.gameObject.AddComponent<CreativeInformationTrial>(); }
            if (!File.Exists(CommandFile)) return;
            string command = File.ReadAllText(CommandFile).Trim(); File.Delete(CommandFile);
            try
            {
                if (command == "build" && !EditorApplication.isPlaying) Build();
                else if(command=="polish"&&!EditorApplication.isPlaying) FrontendPolish.Apply();
                else if(command=="exitbutton"&&!EditorApplication.isPlaying) FrontendExitButtonInstaller.Apply();
                else if(command=="residentui"&&!EditorApplication.isPlaying) ResidentsGameplayInstaller.Apply();
                else if(command=="demo"&&!EditorApplication.isPlaying) DemoSceneInstaller.Apply();
                else if(command=="visualpolish"&&!EditorApplication.isPlaying) ResidentsVisualPolish.Apply();
                else if(command=="playability"&&!EditorApplication.isPlaying) PlayerExperienceInstaller.Apply();
                else if(command=="backgroundfix"&&!EditorApplication.isPlaying) ResidentsVisualPolish.ApplyWholeBackgroundLighting();
                else if(command=="singlebackdrop"&&!EditorApplication.isPlaying) SingleBackdropInstaller.Apply();
                else if(command=="floordetail"&&!EditorApplication.isPlaying) FloorDetailInstaller.Apply();
                else if(command=="endingui"&&!EditorApplication.isPlaying) EndingsInstaller.Apply();
                else if(command=="uipolish"&&!EditorApplication.isPlaying) UIPolishInstaller.Apply();
                else if(command=="scatteredsharing"&&!EditorApplication.isPlaying) ScatteredSharingInstaller.Apply();
                else if(command=="statuscard"&&!EditorApplication.isPlaying) IllustratedStatusCardInstaller.Apply();
                else if(command=="creativebeats"&&!EditorApplication.isPlaying) CreativeBeatsInstaller.Apply();
                else if(command=="motionadditions"&&!EditorApplication.isPlaying) ResidentMotionAdditionsInstaller.Apply();
                else if(command=="statepropmotion"&&!EditorApplication.isPlaying) StatePropMotionInstaller.Apply();
                else if(command=="statepropmotionqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<StatePropMotionSmokeCheck>();
                else if(command=="motionadditionsqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<ResidentMotionAdditionsSmokeCheck>();
                else if(command=="creativebeatsqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<CreativeBeatsSmokeCheck>();
                else if(command=="targetchoiceqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<TargetChoiceProbe>();
                else if(command=="recipientchoiceqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<RecipientChoiceProbe>();
                else if(command=="informationtrial"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<CreativeInformationTrial>();
                else if(command=="statuscardqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<IllustratedStatusCardSmokeCheck>();
                else if(command=="refresh"&&!EditorApplication.isPlaying) AssetDatabase.Refresh();
                else if(command=="musicrefresh"&&!EditorApplication.isPlaying) MusicAssetRefresh.Apply();
                else if(command=="sharingqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<ScatteredSharingSmokeCheck>();
                else if(command=="organizecode"&&!EditorApplication.isPlaying) CodeOrganizationTools.Organize();
                else if(command=="codecheck"&&!EditorApplication.isPlaying) CodeOrganizationTools.Validate();
                else if(command=="uipolishqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<UIPolishSmokeCheck>();
                else if(command=="endingqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<EndingsSmokeCheck>();
                else if(command=="floordetailqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<FloorDetailSmokeCheck>();
                else if(command=="singlebackdropqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<SingleBackdropSmokeCheck>();
                else if(command=="backgroundqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<SingleBackdropSmokeCheck>();
                else if(command=="playabilityqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<PlayerExperienceSmokeCheck>();
                else if(command=="visualqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<VisualPolishSmokeCheck>();
                else if(command=="releaseqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<ReleasePolishSmokeCheck>();
                else if(command=="demoqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<DemoSmokeCheck>();
                else if(command=="residentqa"&&EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<ResidentsGameplaySmokeCheck>();
                else if (command == "play") EditorApplication.isPlaying = true;
                else if (command == "stop") EditorApplication.isPlaying = false;
                else if (command == "qa" && EditorApplication.isPlaying) UnityEngine.Object.FindObjectOfType<FrontendServices>().gameObject.AddComponent<FrontendSmokeCheck>();
                else if (command == "windows" && !EditorApplication.isPlaying) BuildWindows();
                else if (command == "demowindows" && !EditorApplication.isPlaying) BuildWindows(true);
                else if (command == "status") WriteStatus();
                else if(command=="audiodiagnostics"&&EditorApplication.isPlaying)WriteAudioDiagnostics();
                else throw new InvalidOperationException("Unsupported command/state: " + command);
            }
            catch (Exception e) { File.WriteAllText("Library/frontend-failure.txt", e.ToString()); Debug.LogException(e); }
        }
        static void WriteAudioDiagnostics()
        {
            var service=FrontendServices.Instance;var samples=new float[1024];AudioListener.GetOutputData(samples,0);
            var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
            var mute=typeof(EditorUtility).GetProperty("audioMasterMute",flags);
            File.WriteAllText("Library/audio-diagnostics.txt","EditorMute="+(mute==null?"unavailable":mute.GetValue(null).ToString())+"\nListenerPause="+AudioListener.pause+"\nListenerVolume="+AudioListener.volume+
                "\nListeners="+UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(l=>l.enabled)+"\nRate="+AudioSettings.outputSampleRate+"\nPeak="+samples.Max(x=>Mathf.Abs(x))+"\n"+
                string.Join("\n",service.GetComponents<AudioSource>().Select(s=>"clip="+(s.clip?s.clip.name:"none")+" volume="+s.volume+" mute="+s.mute+" playing="+s.isPlaying+" load="+(s.clip?s.clip.loadState.ToString():"none"))));
        }
        static void WriteStatus()
        {
            File.WriteAllText("Library/frontend-status.txt", "playing=" + EditorApplication.isPlaying + "\nscene=" + SceneManager.GetActiveScene().path + "\n" + string.Join("\n", Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(x=>x && x.GetType().Namespace == "EveryoneRecommends.Frontend").Select(x=>x.name + ":" + x.GetType().Name)));
            if(EditorApplication.isPlaying)foreach(var s in UnityEngine.Object.FindObjectsOfType<SettingsView>())
            {
                var slider=s.master;var h=slider.handleRect;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,h.position)};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                File.AppendAllText("Library/frontend-status.txt","\nHandle="+h.rect+" delta="+h.sizeDelta+" scale="+h.lossyScale+" image="+h.GetComponent<Image>().preserveAspect+"\nHits="+string.Join(",",hits.Select(x=>x.gameObject.name+" parent="+x.gameObject.transform.parent.name)));
            }
        }
        [MenuItem("Everyone Recommends/Build Start Menu And Residents Scenes")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes/Backups");
            for (int i=0;i<SceneManager.sceneCount;i++) { var s=SceneManager.GetSceneAt(i); if(s.isDirty) EditorSceneManager.SaveScene(s,"Assets/Scenes/Backups/"+s.name+"_BeforeFrontend.scene",true); }
            foreach(var scenePath in new[]{"Assets/Scenes/MainMenu.scene","Assets/Scenes/Residents.scene"})
                if(File.Exists(scenePath)) { Directory.CreateDirectory("Design/MenuImplementationBackup_20261004"); File.Copy(scenePath,"Design/MenuImplementationBackup_20261004/"+Path.GetFileName(scenePath)+"."+DateTime.Now.ToString("HHmmss")+".copy"); }
            PrepareAssets();
            CreateServices();
            var main = NewScene("MainMenu");
            var menu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MainPath));
            PrefabUtility.UnpackPrefabInstance(menu, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            SetupCanvas(menu.GetComponent<Canvas>());
            var view = menu.AddComponent<MenuView>();
            view.start = Find<Button>(menu, "Button_Start_Normal"); view.howTo = Find<Button>(menu, "Button_HowTo_Normal"); view.settings = Find<Button>(menu, "Button_Settings_Normal"); view.achievements = Find<Button>(menu, "Button_Achievements");
            foreach(var b in new[]{view.start,view.howTo,view.settings,view.achievements}) b.onClick = new Button.ButtonClickedEvent();
            view.achievementsPanel = Find<RectTransform>(menu,"AchievementsPanel").gameObject;
            FrontendPolish.AttachPopup(view.achievementsPanel);
            view.settingsPanel = AddSettings(menu.transform).gameObject;
            CreateHowTo(menu.transform, view);
            CreateEntrance(menu);
            FrontendExitButtonInstaller.Attach(view);
            view.achievementsPanel.SetActive(false); view.settingsPanel.SetActive(false); view.howToPanel.SetActive(false);
            AddSounds(menu);
            EditorSceneManager.SaveScene(main,"Assets/Scenes/MainMenu.scene");
            EditorSceneManager.CloseScene(main,true);
            var residents = NewScene("Residents"); BuildResidents();
            EditorSceneManager.SaveScene(residents,"Assets/Scenes/Residents.scene"); EditorSceneManager.CloseScene(residents,true);
            var all = new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene("Assets/Scenes/MainMenu.scene",true),new EditorBuildSettingsScene("Assets/Scenes/Residents.scene",true)};
            all.AddRange(EditorBuildSettings.scenes.Where(s=>s.path!="Assets/Scenes/MainMenu.scene" && s.path!="Assets/Scenes/Residents.scene"));
            EditorBuildSettings.scenes=all.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.scene",OpenSceneMode.Single);
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); gameView.Show();
            ValidateScene(); File.WriteAllText("Library/frontend-built.txt",DateTime.Now.ToString("O")+"\nMainMenu + Residents saved; original SampleScene preserved.\n");
            Debug.Log("[Frontend] Two independent scenes built and verified.");
        }
        static void PrepareAssets()
        {
            Directory.CreateDirectory("Assets/Art/UI/Fonts");
            const string fontPath="Assets/Art/UI/Fonts/NotoSansSC-Regular.otf";
            if(!File.Exists(fontPath)) throw new FileNotFoundException("Licensed Noto Sans SC font is missing; restore it with its OFL notice.", fontPath);
            AssetDatabase.ImportAsset(fontPath); Font=AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            foreach(string path in Directory.GetFiles("Assets/Art/ImportedGame","*.png"))
            {
                string p=path.Replace('\\','/'); AssetDatabase.ImportAsset(p);
                var t=(TextureImporter)AssetImporter.GetAtPath(p);
                t.textureType=TextureImporterType.Sprite; t.spriteImportMode=SpriteImportMode.Single; t.mipmapEnabled=false; t.alphaIsTransparency=true; t.wrapMode=TextureWrapMode.Clamp; t.textureCompression=TextureImporterCompression.Uncompressed; t.maxTextureSize=2048;
                if(p.Contains("B03_")){t.textureType=TextureImporterType.Cursor;t.isReadable=true;t.maxTextureSize=64;}
                t.SaveAndReimport();
            }
            Directory.CreateDirectory("Assets/Audio/Music"); Directory.CreateDirectory("Assets/Audio/SFX");
            WriteAudio("Assets/Audio/Music/MenuGentleLoop.wav",true); WriteAudio("Assets/Audio/SFX/MenuClick.wav",false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        static void WriteAudio(string path,bool music)
        {
            if(File.Exists(path))return;
            const int rate=22050; int count=(int)(rate*(music?16:.09));
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+count*2);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));stream.Write(16);stream.Write((short)1);stream.Write((short)1);stream.Write(rate);stream.Write(rate*2);stream.Write((short)2);stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(count*2);
                double[] notes={261.626,329.628,391.995,329.628,293.665,349.228,440,349.228};
                for(int i=0;i<count;i++)
                {
                    double t=(double)i/rate,v;
                    if(music){double local=t%2; double envelope=Math.Sin(Math.PI*local/2); double f=notes[Math.Min(7,(int)(t/2))];v=(Math.Sin(2*Math.PI*f*t)+.35*Math.Sin(2*Math.PI*f*.5*t))*envelope*envelope*.055;}
                    else v=Math.Sin(2*Math.PI*(900*t-1400*t*t))*Math.Exp(-t*65)*Math.Min(1,t/.004)*.23;
                    stream.Write((short)(v*32767));
                }
            }
        }
        static void CreateServices()
        {
            var go=new GameObject("FrontendServices");var service=go.AddComponent<FrontendServices>();var wipe=go.AddComponent<CharacterSceneWipe>();service.transition=wipe;
            service.cursorTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/ImportedGame/B03_点击指针.png");
            service.musicClip=AssetDatabase.LoadAssetAtPath<AudioClip>(FrontendPolish.MusicPath)??AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/MenuGentleLoop.wav");service.clickClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/MenuClick.wav");
            string[] names={"U03_猫_开心","U08_美食_期待","U04_游戏_专注","U02_新闻_好奇","U01_音乐_闭眼微笑"};
            wipe.characters=names.Select(n=>Sprite("Assets/Art/ImportedGame/"+n+".png")).ToArray();
            PrefabUtility.SaveAsPrefabAsset(go,ServicesPath);UnityEngine.Object.DestroyImmediate(go);
        }
        static Scene NewScene(string name)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";var c=camera.GetComponent<Camera>();c.orthographic=true;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Cream;camera.transform.position=new Vector3(0,0,-10);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ServicesPath));return scene;
        }
        static void SetupCanvas(Canvas c)
        {
            c.renderMode=RenderMode.ScreenSpaceOverlay;var s=c.GetComponent<CanvasScaler>()??c.gameObject.AddComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1672,941);s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        }
        static T Find<T>(GameObject root,string name) where T:Component
        {
            var target=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
            if(!target)throw new Exception("Missing UI object: "+name);return target.GetComponent<T>();
        }
        static Sprite Sprite(string path){var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!s)throw new Exception("Missing sprite: "+path);return s;}
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=new Vector2(.5f,.5f);t.anchoredPosition=pos;t.sizeDelta=size;return t;
        }
        static Image Picture(string name,Transform parent,Sprite sprite,Vector2 pos,Vector2 size,Color? color=null)
        {
            var t=Rect(name,parent,pos,size);var i=t.gameObject.AddComponent<Image>();i.sprite=sprite;i.color=color??Color.white;i.raycastTarget=false;return i;
        }
        static Text Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize=30)
        {
            var t=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();t.font=Font;t.text=text;t.fontSize=fontSize;t.color=new Color(.1f,.085f,.075f);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
        }
        static Button Button(string name,Transform parent,string label,Vector2 pos,Vector2 size)
        {
            var art=Root+"SettingsUI/Art/03_按钮底板/Button_Secondary_";
            var image=Picture(name,parent,Sprite(art+"Normal.png"),pos,size);image.type=Image.Type.Simple;image.raycastTarget=true;
            var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.SpriteSwap;b.spriteState=new SpriteState{highlightedSprite=Sprite(art+"Hover.png"),pressedSprite=Sprite(art+"Pressed.png"),disabledSprite=Sprite(art+"Disabled.png")};
            Label("Label",b.transform,label,Vector2.zero,size-new Vector2(20,8),28);return b;
        }
        static SettingsView AddSettings(Transform parent)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath),parent);
            PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            FrontendPolish.AttachPopup(go);
            var s=go.AddComponent<SettingsView>();s.master=Find<Slider>(go,"MasterSlider");s.music=Find<Slider>(go,"MusicSlider");s.sfx=Find<Slider>(go,"SFXSlider");s.fullscreen=Find<Toggle>(go,"FullscreenToggle");s.motion=Find<Toggle>(go,"MotionToggle");s.close=Find<Button>(go,"Close");s.back=Find<Button>(go,"Back");s.reset=Find<Button>(go,"RestoreDefaults");
            foreach(var b in new[]{s.close,s.back,s.reset})b.onClick=new Button.ButtonClickedEvent();
            foreach(var slider in new[]{s.master,s.music,s.sfx})
            {
                // Slider drives the vertical anchors to stretch; zero delta preserves a round 54px handle.
                slider.handleRect.sizeDelta=new Vector2(54,0);
                slider.handleRect.GetComponent<Image>().preserveAspect=true;
                EditorUtility.SetDirty(slider.handleRect);EditorUtility.SetDirty(slider.handleRect.GetComponent<Image>());
                var track=slider.transform.Find("Track").GetComponent<Image>();track.raycastTarget=true;EditorUtility.SetDirty(track);
            }
            s.masterValue=Label("MasterPercent",s.master.transform,"80%",new Vector2(280,0),new Vector2(100,50),25);
            s.musicValue=Label("MusicPercent",s.music.transform,"65%",new Vector2(280,0),new Vector2(100,50),25);
            s.sfxValue=Label("SFXPercent",s.sfx.transform,"80%",new Vector2(280,0),new Vector2(100,50),25);
            AddSounds(go);return s;
        }
        static void CreateHowTo(Transform parent,MenuView view)
        {
            var panel=Rect("HowToPanel",parent,Vector2.zero,Vector2.zero);panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;
            var scrim=Picture("Scrim",panel,null,Vector2.zero,Vector2.zero,new Color(0,0,0,.43f));scrim.rectTransform.anchorMin=Vector2.zero;scrim.rectTransform.anchorMax=Vector2.one;scrim.raycastTarget=true;
            var bg=Picture("Panel",panel,Sprite(Root+"SettingsUI/Art/01_底板/Panel_Cream.png"),Vector2.zero,new Vector2(1100,750));bg.type=Image.Type.Sliced;bg.raycastTarget=true;
            Label("Heading",panel,"怎么玩",new Vector2(0,275),new Vector2(700,80),48);
            view.pageTitle=Label("PageTitle",panel,"",new Vector2(0,145),new Vector2(920,80),38);
            view.pageBody=Label("PageBody",panel,"",new Vector2(0,-20),new Vector2(880,260),30);view.pageBody.lineSpacing=1.4f;
            view.pageNumber=Label("PageNumber",panel,"",new Vector2(0,-205),new Vector2(180,55),25);
            view.previousPage=Button("Previous",panel,"上一页",new Vector2(-330,-275),new Vector2(195,70));view.nextPage=Button("Next",panel,"下一页",new Vector2(330,-275),new Vector2(195,70));view.howToBack=Button("Back",panel,"返回",new Vector2(0,-275),new Vector2(195,70));view.howToClose=Button("Close",panel,"×",new Vector2(475,295),new Vector2(64,64));view.howToPanel=panel.gameObject;
        }
        static void CreateEntrance(GameObject menu)
        {
            var animators=menu.GetComponentsInChildren<Animator>(true);if(animators.Length!=5)throw new Exception("Expected exactly five character groups; got "+animators.Length);
            var entry=menu.AddComponent<MenuEntrance>();entry.groups=new RectTransform[5];entry.fades=new CanvasGroup[5];entry.circles=new RectTransform[5];entry.circleFades=new CanvasGroup[5];entry.animators=animators;
            var colors=new[]{new Color(1,.48f,.38f),new Color(1,.76f,.3f),new Color(.3f,.75f,.67f),new Color(.62f,.45f,.8f),new Color(.38f,.55f,.88f)};
            for(int i=0;i<5;i++)
            {
                var original=(RectTransform)animators[i].transform;var parent=original.parent;int sibling=original.GetSiblingIndex();
                var wrapper=Rect("Entrance_"+original.name,parent,original.anchoredPosition,original.sizeDelta);wrapper.localScale=original.localScale;wrapper.anchorMin=original.anchorMin;wrapper.anchorMax=original.anchorMax;wrapper.pivot=original.pivot;wrapper.SetSiblingIndex(sibling);
                original.SetParent(wrapper,false);original.anchorMin=original.anchorMax=new Vector2(.5f,.5f);original.anchoredPosition=Vector2.zero;original.localScale=Vector3.one;
                entry.groups[i]=wrapper;entry.fades[i]=wrapper.gameObject.AddComponent<CanvasGroup>();
                var circle=Rect("CircleReveal_"+i,parent,wrapper.anchoredPosition,new Vector2(405,405));circle.SetSiblingIndex(wrapper.GetSiblingIndex());var graphic=circle.gameObject.AddComponent<SoftCircleGraphic>();graphic.color=colors[i];graphic.raycastTarget=false;
                entry.circles[i]=circle;entry.circleFades[i]=circle.gameObject.AddComponent<CanvasGroup>();entry.circleFades[i].alpha=.2f;
                animators[i].updateMode=AnimatorUpdateMode.UnscaledTime;
            }
        }
        static void AddSounds(GameObject root)
        {
            foreach(var s in root.GetComponentsInChildren<Selectable>(true))if(!s.GetComponent<UiClickSound>())s.gameObject.AddComponent<UiClickSound>();
        }
        static void BuildResidents()
        {
            var canvas=new GameObject("ResidentsCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));SetupCanvas(canvas.GetComponent<Canvas>());
            var art=Rect("Artboard_1672x941",canvas.transform,Vector2.zero,new Vector2(1672,941));
            Picture("BuildingArtwork",art,Sprite("Assets/Art/ImportedGame/住宅全景与固定UI.png"),Vector2.zero,new Vector2(1672,941));
            Picture("LiveStatusPanel",art,null,new Vector2(620,0),new Vector2(448,941),Cream);
            var view=canvas.AddComponent<ResidentsView>();
            Label("Heading",art,"全楼动态",new Vector2(620,388),new Vector2(420,80),48);
            view.selectionLabel=Label("TopicSelection",art,"今天想看什么？",new Vector2(620,315),new Vector2(420,55),27);
            string[] actorNames={"P01_音乐_闭眼听歌","P02_新闻_专心看报","P03_猫_看平板","P04_游戏_专注操作","P05_美食_吃饭刷手机","P06_音乐_沉浸听歌","P07_猫_伸手拥抱","P08_美食_开心吃饭","P09_游戏_认真操作","P10_新闻_展开报纸"};
            string[] iconNames={"S01_猫","S02_美食","S03_游戏","S04_新闻","S05_音乐"};
            view.activities=actorNames.Select(n=>Sprite("Assets/Art/ImportedGame/"+n+".png")).ToArray();view.topicIcons=iconNames.Select(n=>Sprite("Assets/Art/ImportedGame/"+n+".png")).ToArray();
            view.residents=new Image[9];view.statusIcons=new Image[9];int[] initial={4,3,0,2,1,4,0,1,2};
            for(int i=0;i<9;i++)
            {
                int row=i<3?0:i<6?1:2,col=i<3?i:i<6?i-3:i-6;
                float x=row==2?280+col*230:320+col*275;float y=255+row*240;
                view.residents[i]=Picture("Resident_"+(i+1),art,view.activities[i],new Vector2(x-836,470.5f-y),new Vector2(152,158));view.residents[i].preserveAspect=true;
                float nodeX=495+col*125;float nodeY=205-row*135;
                view.statusIcons[i]=Picture("Status_"+(i+1),art,view.topicIcons[initial[i]],new Vector2(nodeX,nodeY),new Vector2(68,68));view.statusIcons[i].preserveAspect=true;
            }
            view.floors=new Button[3];for(int i=0;i<3;i++)view.floors[i]=Button("Floor_"+(i+1),art,(i+1)+"F",new Vector2(410,-65+i*135),new Vector2(65,50));
            Label("Prompt",art,"今天想看什么？",new Vector2(620,-185),new Vector2(420,65),35);
            view.topics=new Button[5];string[] topicLabels={"猫","美食","游戏","新闻","音乐"};
            for(int i=0;i<5;i++)
            {
                var pos=i<3?new Vector2(475+i*142,-278):new Vector2(550+(i-3)*145,-370);
                var b=Button("Topic_"+i,art,"",pos,new Vector2(130,82));var label=b.GetComponentInChildren<Text>();label.text=topicLabels[i];label.fontSize=22;label.rectTransform.anchoredPosition=new Vector2(0,-22);label.rectTransform.sizeDelta=new Vector2(110,30);
                var icon=Picture("Icon",b.transform,view.topicIcons[i],new Vector2(0,10),new Vector2(43,43));icon.preserveAspect=true;view.topics[i]=b;
            }
            var feedbackBackground=Picture("FeedbackBackground",art,null,new Vector2(-200,-380),new Vector2(880,50),new Color(Cream.r,Cream.g,Cream.b,.92f));
            view.feedback=Label("Feedback",art,"",new Vector2(-200,-380),new Vector2(850,50),24);
            view.allFloors=Button("AllFloors",art,"全楼视图",new Vector2(-730,-430),new Vector2(175,55));
            view.returnToMenu=Button("ReturnToMenu",art,"返回首页",new Vector2(505,-438),new Vector2(150,46));view.settingsButton=Button("Settings",art,"设置",new Vector2(705,-438),new Vector2(125,46));
            view.settingsPanel=AddSettings(canvas.transform).gameObject;view.settingsPanel.SetActive(false);AddSounds(canvas);
            ResidentsGameplayInstaller.Attach(view);
        }
        static void ValidateScene()
        {
            int missing=0;foreach(var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if(missing>0)throw new Exception("Missing scripts: "+missing);
            var view=UnityEngine.Object.FindObjectOfType<MenuView>();if(!view||!view.start||!view.settingsPanel)throw new Exception("Menu wiring failed.");
            var entry=UnityEngine.Object.FindObjectOfType<MenuEntrance>();if(entry.groups.Length!=5)throw new Exception("Wrong group count.");
            File.WriteAllText("Library/frontend-validation.txt","Missing scripts: 0\nFive independent entrance groups: 5\nSettings sliders: 3\nSettings toggles: 2\nTwo scenes: MainMenu, Residents\n");
        }
        static void BuildWindows(bool demo=false)
        {
            string folder=demo?"Builds/Windows/Demo":"Builds/Windows/MenuPreview";
            Directory.CreateDirectory(folder);
            var options=BuildOptions.CompressWithLz4|(demo?BuildOptions.None:BuildOptions.Development);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainMenu.scene","Assets/Scenes/Residents.scene"},locationPathName=folder+"/EveryoneRecommends.exe",target=BuildTarget.StandaloneWindows64,options=options});
            File.WriteAllText("Library/frontend-build-result.txt",result.summary.result+"\nErrors="+result.summary.totalErrors+"\nWarnings="+result.summary.totalWarnings);
            Directory.CreateDirectory("Builds/Verification/Demo");
            File.WriteAllLines("Builds/Verification/Demo/BuildMessages.txt",result.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.content));
        }
    }
}
