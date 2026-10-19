using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EveryoneRecommends.Frontend;

namespace EveryoneRecommends.EditorTools
{
    public static class DemoSceneInstaller
    {
        const string Ui="Assets/EveryoneRecommends/SettingsUI/Art/";
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        [MenuItem("Everyone Recommends/Install Playable Demo")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing Demo.");
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
            File.Copy("Assets/Scenes/Residents.scene",backup+"/Residents.scene");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Residents.scene");
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Residents.scene",OpenSceneMode.Single);
            if(scene.isDirty)EditorSceneManager.SaveScene(scene,backup+"/Residents_Unsaved.scene",true);
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            Attach(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Library/demo-install.txt",DateTime.Now.ToString("O")+"\nPlayable Demo installed; original actor coordinates preserved.\nBackup="+backup);
            Debug.Log("[Demo] Full playable scene installed.");
        }
        public static void Attach(ResidentsView view)
        {
            var art=view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Artboard_1672x941");
            if(view.overlay) { Polish(view);return; }
            var viewport=Rect("BuildingViewport",art,new Vector2(-220,0),new Vector2(1228,941));viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("BuildingContent",viewport,new Vector2(220,0),new Vector2(1672,941));
            var background=art.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="BuildingArtwork");background.SetParent(content,false);
            view.buildingViewport=viewport;view.buildingContent=content;
            view.buildingTint=Image("DayNightTint",content,null,Vector2.zero,new Vector2(1672,941));view.buildingTint.color=Color.clear;
            string[] motionNames={"Cat","Food","Game","News","Music"};
            var controllers=motionNames.Select(n=>AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/EveryoneRecommends/ResidentMotion/"+n+"/"+n+"_UI.controller")).ToArray();
            if(controllers.Any(c=>!c))throw new InvalidOperationException("Resident motion controllers not imported.");
            int count=view.residents.Length;view.motions=new ResidentMotion[count];view.reactionLabels=new Text[count];view.recommendedLabels=new Text[count];view.propImages=new Image[count*5];
            string[] props={"E01_猫_完整趴卧","E07_桌面_杯碗组合","S03_游戏","S04_新闻","E04_音乐_双音符"};
            for(int i=0;i<count;i++)
            {
                view.residents[i].transform.SetParent(content,false);
                var driver=view.residents[i].GetComponent<ResidentMotion>();if(!driver)driver=view.residents[i].gameObject.AddComponent<ResidentMotion>();
                driver.target=view.residents[i];driver.animator=view.residents[i].GetComponent<Animator>();driver.animator.enabled=false;
                driver.controllers=controllers;driver.owner=view;view.motions[i]=driver;
                view.reactionLabels[i]=Label("VisibleResponse_"+i,view.residents[i].transform,"",new Vector2(0,104),new Vector2(215,64),18);
                view.reactionLabels[i].gameObject.SetActive(false);
                view.recommendedLabels[i]=Label("Recommended_"+i,view.statusIcons[i].transform,"",new Vector2(0,-43),new Vector2(95,28),16);
                view.recommendedLabels[i].color=new Color(.22f,.55f,.43f);
                for(int t=0;t<5;t++)
                {
                    var image=Image("LifeProp_"+i+"_"+t,view.residents[i].transform,Sprite("Assets/Art/ImportedGame/"+props[t]+".png"),new Vector2(-52+t*25,-83),new Vector2(30,30));
                    image.preserveAspect=true;image.gameObject.SetActive(false);view.propImages[i*5+t]=image;
                }
            }
            viewport.SetAsFirstSibling();
            view.statusCard.gameObject.name="LegacyStatusCard_DebugOnly";view.statusCard.gameObject.SetActive(false);
            var cardRoot=Rect("ObservationCard",art,Vector2.zero,new Vector2(330,295));
            var cardPaper=cardRoot.gameObject.AddComponent<Image>();cardPaper.sprite=Sprite(Ui+"01_底板/Panel_Cream.png");cardPaper.type=UnityEngine.UI.Image.Type.Sliced;cardPaper.raycastTarget=true;
            var card=cardRoot.gameObject.AddComponent<ResidentStatusCard>();card.owner=view;card.placementArea=art;
            card.reservedAreas=new[]{(RectTransform)view.stageProgress.transform,view.feedback.rectTransform,(RectTransform)view.allFloors.transform,art.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="LiveStatusPanel")};
            card.number=Label("ResidentName",cardRoot,"住户",new Vector2(-35,118),new Vector2(240,32),24);
            card.close=Button("Close",cardRoot,"×",new Vector2(136,118),new Vector2(34,32),24);
            card.portrait=Image("Portrait",cardRoot,null,new Vector2(-115,62),new Vector2(62,62));card.portrait.preserveAspect=true;
            card.preference=Image("ActivityIcon",cardRoot,null,new Vector2(-112,-4),new Vector2(34,34));card.preference.preserveAspect=true;
            card.details=Label("ObservableState",cardRoot,"",new Vector2(35,21),new Vector2(226,171),17);card.details.alignment=TextAnchor.UpperLeft;card.details.lineSpacing=1.15f;
            card.confirm=Button("ConfirmRecommendation",cardRoot,"先选择主题",new Vector2(0,-85),new Vector2(286,38),22);card.confirmLabel=card.confirm.GetComponentInChildren<Text>();
            card.decline=Button("Decline",cardRoot,"暂不推荐",new Vector2(0,-128),new Vector2(286,32),20);
            view.statusCard=card;cardRoot.gameObject.SetActive(false);
            view.targetLabel=Label("RunTarget",art,"本局目标：尚未选择",new Vector2(620,345),new Vector2(420,44),25);
            view.upFloor=Button("UpFloor",art,"↑ 上一层",new Vector2(-739,352),new Vector2(170,40),21);
            view.downFloor=Button("DownFloor",art,"↓ 下一层",new Vector2(-553,352),new Vector2(170,40),21);
            view.pauseButton=Button("PauseWorld",art,"暂停",new Vector2(130,350),new Vector2(110,40),21);view.pauseLabel=view.pauseButton.GetComponentInChildren<Text>();
            view.fastForward=Button("FastForward",art,"结束本阶段",new Vector2(297,350),new Vector2(210,40),21);
            view.overlay=CreateOverlay(view.transform);
            view.overlay.gameObject.SetActive(false);
            var prompt=art.GetComponentsInChildren<Text>(true).First(t=>t.name=="Prompt");prompt.text="准备推荐主题";
            EditorUtility.SetDirty(view);
            Polish(view);
        }
        static void Polish(ResidentsView view)
        {
            if(!view.changeNotice)
            {
                var art=view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Artboard_1672x941");
                view.changeNotice=Button("OffscreenChange",art,"楼层有新动态 →",new Vector2(-95,350),new Vector2(284,40),20);
                view.changeNoticeLabel=view.changeNotice.GetComponentInChildren<Text>();view.changeNotice.gameObject.SetActive(false);
            }
            var card=view.statusCard;var rect=(RectTransform)card.transform;rect.sizeDelta=new Vector2(260,295);
            card.reservedAreas=new[]{(RectTransform)view.stageProgress.transform,view.feedback.rectTransform,(RectTransform)view.allFloors.transform,
                (RectTransform)view.upFloor.transform,(RectTransform)view.downFloor.transform,(RectTransform)view.pauseButton.transform,(RectTransform)view.fastForward.transform,
                (RectTransform)view.changeNotice.transform,view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="LiveStatusPanel")};
            card.GetComponent<Image>().type=UnityEngine.UI.Image.Type.Simple;
            card.number.rectTransform.anchoredPosition=new Vector2(-20,116);card.number.rectTransform.sizeDelta=new Vector2(185,30);card.number.fontSize=20;
            ((RectTransform)card.close.transform).anchoredPosition=new Vector2(109,116);((RectTransform)card.close.transform).sizeDelta=new Vector2(30,30);
            card.close.GetComponentInChildren<Text>().rectTransform.sizeDelta=new Vector2(28,28);
            card.portrait.rectTransform.anchoredPosition=new Vector2(-72,67);card.portrait.rectTransform.sizeDelta=new Vector2(62,62);
            card.preference.rectTransform.anchoredPosition=new Vector2(70,67);card.preference.rectTransform.sizeDelta=new Vector2(46,46);
            card.details.rectTransform.anchoredPosition=new Vector2(0,-22);card.details.rectTransform.sizeDelta=new Vector2(230,111);card.details.fontSize=14;
            var confirm=(RectTransform)card.confirm.transform;confirm.anchoredPosition=new Vector2(0,-91);confirm.sizeDelta=new Vector2(230,34);
            card.confirmLabel.rectTransform.sizeDelta=new Vector2(220,30);card.confirmLabel.fontSize=19;
            var decline=(RectTransform)card.decline.transform;decline.anchoredPosition=new Vector2(0,-128);decline.sizeDelta=new Vector2(230,30);
            var label=card.decline.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=new Vector2(220,26);label.fontSize=18;
            if(!view.overlay.paper)view.overlay.paper=view.overlay.transform.Find("Paper") as RectTransform;
            if(!view.overlay.scrim)view.overlay.scrim=view.overlay.transform.Find("Scrim").GetComponent<Image>();
            EditorUtility.SetDirty(card);EditorUtility.SetDirty(view.overlay);EditorUtility.SetDirty(view);
            ResidentsVisualPolish.Attach(view);
            PlayerExperienceInstaller.Attach(view);
        }
        static DemoOverlay CreateOverlay(Transform parent)
        {
            var root=Rect("DemoPaperOverlay",parent,Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;
            var scrim=Image("Scrim",root,null,Vector2.zero,Vector2.zero);scrim.rectTransform.anchorMin=Vector2.zero;scrim.rectTransform.anchorMax=Vector2.one;scrim.color=new Color(0,0,0,.28f);scrim.raycastTarget=true;
            var paper=Image("Paper",root,Sprite(Ui+"01_底板/Panel_Cream.png"),Vector2.zero,new Vector2(920,690));paper.type=UnityEngine.UI.Image.Type.Sliced;paper.raycastTarget=true;
            var overlay=root.gameObject.AddComponent<DemoOverlay>();
            overlay.paper=paper.rectTransform;overlay.scrim=scrim;
            overlay.heading=Label("Heading",paper.transform,"",new Vector2(0,282),new Vector2(820,74),36);
            overlay.body=Label("Body",paper.transform,"",new Vector2(0,28),new Vector2(810,407),22);overlay.body.alignment=TextAnchor.UpperLeft;overlay.body.lineSpacing=1.15f;
            overlay.buttons=new Button[8];overlay.labels=new Text[8];
            for(int i=0;i<8;i++)
            {
                var button=Button("Action_"+i,paper.transform,"",new Vector2(-315+(i%4)*210,-238-(i/4)*62),new Vector2(198,52),23);
                overlay.buttons[i]=button;overlay.labels[i]=button.GetComponentInChildren<Text>();
            }
            return overlay;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));go.layer=5;
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=pos;rect.sizeDelta=size;return rect;
        }
        static Image Image(string name,Transform parent,Sprite sprite,Vector2 pos,Vector2 size)
        {
            var image=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;
        }
        static Text Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize)
        {
            var label=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();label.font=Font;label.fontSize=fontSize;label.text=text;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.1f,.09f,.08f);label.raycastTarget=false;return label;
        }
        static Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize)
        {
            var image=Image(name,parent,Sprite(Ui+"03_按钮底板/Button_Secondary_Normal.png"),pos,size);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=Sprite(Ui+"03_按钮底板/Button_Secondary_Hover.png"),pressedSprite=Sprite(Ui+"03_按钮底板/Button_Secondary_Pressed.png"),disabledSprite=Sprite(Ui+"03_按钮底板/Button_Secondary_Disabled.png")};
            Label("Label",image.transform,text,Vector2.zero,size-new Vector2(12,4),fontSize);image.gameObject.AddComponent<UiClickSound>();return button;
        }
        static Sprite Sprite(string path) { var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!sprite)throw new InvalidOperationException("Missing Demo art: "+path);return sprite; }
    }
}
