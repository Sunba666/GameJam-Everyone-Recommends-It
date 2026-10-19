using System;
using System.IO;
using System.Linq;
using EveryoneRecommends.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EveryoneRecommends.EditorTools
{
    public static class CreativeBeatsInstaller
    {
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        static readonly Color Ink=new Color(.13f,.10f,.08f),Cream=new Color(1,.965f,.882f);
        [MenuItem("Everyone Recommends/Open Short Information Trial")]
        public static void OpenInformationTrial()
        {
            SessionState.SetBool("EveryoneRecommends.PendingInformationTrial",true);
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
        }
        [MenuItem("Everyone Recommends/Install Creative Life Beats")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing creative beats.");
            const string path="Assets/Scenes/Residents.scene";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_CreativeBeats";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            IllustratedStatusCardInstaller.Attach(owner);Attach(owner);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(opened)EditorSceneManager.CloseScene(scene,true);
            File.WriteAllText("Library/creative-beats-install.txt",DateTime.Now.ToString("O")+"\nOne cat-care contrast, read-only spread clip, information comparison and three daily wishes installed.\nBackup="+backup);
        }
        public static void Attach(ResidentsView owner)
        {
            var art=owner.statusCard.placementArea;
            AttachCard(owner);AttachCat(owner);AttachSpread(owner,art);
            string[] descriptions={
                "猫话题成为榜首。谁还想继续看，谁已经把注意力转回自己的生活？这栋楼的记录会回答。",
                "美食成了最多住户愿意继续接触的话题。热闹来自你的推荐，还是邻居的分享？沿着记录找找看。",
                "游戏话题独占第一。再看看各个房间：谁还愿意参与，谁已经转向别的事？",
                "新闻成了楼里的头号话题。是谁分享给了谁，又是谁看过后改变了活动？回看这一路。",
                "音乐话题登顶了。谁愿意继续听，这段兴趣又经过哪些邻居？最后的生活和分享记录就在下面。",
                "最高人数并列，楼里没有唯一榜首。各有所好，也各有来路；看看这次最后留下的生活。"};
            foreach(var ending in owner.endings.endings)if(ending.description.StartsWith("最后一轮结束"))ending.description=descriptions[(int)ending.id];
            EditorUtility.SetDirty(owner.endings);
            foreach(int index in ResidentLifeStories.FocusResidents)if(owner.roomLabels!=null&&index<owner.roomLabels.Length&&owner.roomLabels[index])
            { owner.roomLabels[index].text=ResidentLifeStories.FocusLabel(index);owner.roomLabels[index].fontSize=14;owner.roomLabels[index].rectTransform.sizeDelta=new Vector2(110,26); }
            EditorUtility.SetDirty(owner);
        }
        public static void AttachCard(ResidentsView owner)
        {
            var card=owner.statusCard;var root=(RectTransform)card.transform;var plate=root.Find("IllustratedPlate");
            var panel=Rect("ClueView",plate,Vector2.zero,new Vector2(340,425));
            var paper=Picture("Paper",panel,null,new Vector2(0,-70.5f),new Vector2(279,205));paper.color=Cream;
            var header=Label("Heading",paper.transform,"最近看到的反应",new Vector2(0,85),new Vector2(264,24),17);
            card.clueLines=new Text[5];
            for(int t=0;t<5;t++)
            {
                float y=51-t*31;Picture("Topic_"+t,paper.transform,owner.topicIcons[t],new Vector2(-115,y),new Vector2(27,27));
                card.clueLines[t]=Label("Observed_"+t,paper.transform,"还没看到接收反应",new Vector2(20,y),new Vector2(216,26),15);
            }
            var legend=Picture("LegendPaper",panel,null,new Vector2(0,-188),new Vector2(278,22));legend.color=Cream;
            Label("Legend",legend.transform,"看动作，再猜原因",Vector2.zero,new Vector2(260,20),14);
            card.cluePanel=panel.gameObject;panel.gameObject.SetActive(false);
            card.informationToggle=Button("InformationMode",root,"信息：完整条格",new Vector2(83,-162),new Vector2(172,22),14);
            card.informationLabel=card.informationToggle.GetComponentInChildren<Text>();
            Box(card.pauseNote.rectTransform,new Vector2(-87,-162),new Vector2(160,22));card.pauseNote.fontSize=13;
            Box(card.recent.rectTransform,new Vector2(-34,-220),new Vector2(268,20));card.recent.fontSize=14;
            card.life=Button("DailyWish",root,"日常",new Vector2(139,-220),new Vector2(60,22),15);
            card.life.gameObject.SetActive(false);EditorUtility.SetDirty(card);
        }
        static void AttachCat(ResidentsView owner)
        {
            var beat=owner.GetComponent<CatCareBeatView>();if(!beat)beat=owner.gameObject.AddComponent<CatCareBeatView>();
            beat.owner=owner;beat.residentIndex=0;var actor=owner.residents[0];
            beat.stage=Rect("CatCareContrast",actor.transform,Vector2.zero,actor.rectTransform.sizeDelta);
            beat.body=Picture("Body",beat.stage,null,new Vector2(-15,0),new Vector2(202,202));beat.body.raycastTarget=true;
            beat.careOutline=beat.body.GetComponent<Outline>();if(!beat.careOutline)beat.careOutline=beat.body.gameObject.AddComponent<Outline>();beat.careOutline.effectDistance=new Vector2(2,-2);beat.careOutline.enabled=false;
            beat.cat=Picture("PhysicalCat",beat.stage,Sprite("E02_猫_坐姿可拥抱"),new Vector2(66,-44),new Vector2(108,108));
            beat.swipedCard=Picture("SwipedVideo",beat.stage,Sprite("E06_猫推送卡"),new Vector2(36,-23),new Vector2(55,66));
            beat.tabletPose=Sprite("P03_猫_看平板");beat.carePose=Sprite("P07_猫_伸手拥抱");beat.originalCatProp=owner.propImages[0];
            var captionPaper=Picture("CaptionPaper",beat.stage,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EveryoneRecommends/SettingsUI/Art/03_按钮底板/Button_Secondary_Normal.png"),new Vector2(0,116),new Vector2(206,38));captionPaper.type=Image.Type.Simple;captionPaper.preserveAspect=false;
            beat.caption=Label("Caption",captionPaper.transform,"视频先不看，猫还要照顾",Vector2.zero,new Vector2(190,32),14);
            beat.stage.gameObject.SetActive(false);owner.catCare=beat;EditorUtility.SetDirty(beat);
        }
        static void AttachSpread(ResidentsView owner,RectTransform art)
        {
            var story=owner.GetComponent<SpreadStoryView>();if(!story)story=owner.gameObject.AddComponent<SpreadStoryView>();story.owner=owner;
            var root=Rect("SpreadStory",art,Vector2.zero,new Vector2(1672,941));
            var hit=root.GetComponent<Image>();if(!hit)hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            story.panel=root.GetComponent<CanvasGroup>();if(!story.panel)story.panel=root.gameObject.AddComponent<CanvasGroup>();
            var paper=Picture("Paper",root,owner.overlay.paper.GetComponent<Image>().sprite,new Vector2(620,0),new Vector2(410,650));
            paper.preserveAspect=false;
            story.heading=Label("Heading",paper.transform,"看看怎么传来的",new Vector2(0,267),new Vector2(360,50),25);
            story.clockLabel=Label("Clock",paper.transform,"已发生的记录",new Vector2(0,225),new Vector2(346,24),16);
            story.theme=Picture("Theme",paper.transform,null,new Vector2(0,168),new Vector2(50,50));
            story.statusLabel=Label("Source",paper.transform,"",new Vector2(0,110),new Vector2(344,58),19);
            story.body=Label("Body",paper.transform,"",new Vector2(0,-33),new Vector2(340,220),18);story.body.alignment=TextAnchor.UpperLeft;
            story.pause=Button("Pause",paper.transform,"暂停片段",new Vector2(-85,-209),new Vector2(160,46),19);story.pauseLabel=story.pause.GetComponentInChildren<Text>();
            story.skip=Button("Skip",paper.transform,"直接看结果",new Vector2(85,-209),new Vector2(160,46),19);
            story.back=Button("Back",paper.transform,"返回总结",new Vector2(0,-268),new Vector2(330,44),19);
            var subtitlePaper=Picture("SubtitlePaper",root,null,new Vector2(-190,-365),new Vector2(910,58));subtitlePaper.color=new Color(1,.965f,.882f,.95f);
            story.subtitle=Label("Subtitle",subtitlePaper.transform,"",Vector2.zero,new Vector2(878,50),23);
            var arrowRoot=Rect("StoryArrow",root,Vector2.zero,new Vector2(1672,941));story.arrow=arrowRoot.GetComponent<DeliveryArrowGraphic>();if(!story.arrow)story.arrow=arrowRoot.gameObject.AddComponent<DeliveryArrowGraphic>();story.arrow.raycastTarget=false;
            arrowRoot.SetSiblingIndex(1);arrowRoot.gameObject.SetActive(false);root.gameObject.SetActive(false);root.SetAsLastSibling();owner.spreadStory=story;EditorUtility.SetDirty(story);
        }
        static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ImportedGame/"+name+".png");
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var old=parent.Find(name);var rect=old?(RectTransform)old:new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.gameObject.layer=5;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.localScale=Vector3.one;Box(rect,position,size);return rect;
        }
        static Image Picture(string name,Transform parent,Sprite sprite,Vector2 position,Vector2 size)
        { var rect=Rect(name,parent,position,size);var image=rect.GetComponent<Image>();if(!image)image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=Color.white;image.preserveAspect=sprite;image.raycastTarget=false;return image; }
        static Text Label(string name,Transform parent,string content,Vector2 position,Vector2 size,int fontSize)
        { var rect=Rect(name,parent,position,size);var label=rect.GetComponent<Text>();if(!label)label=rect.gameObject.AddComponent<Text>();label.font=Font;label.fontSize=fontSize;label.text=content;label.color=Ink;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.resizeTextForBestFit=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label; }
        static Button Button(string name,Transform parent,string content,Vector2 position,Vector2 size,int fontSize)
        {
            var image=Picture(name,parent,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EveryoneRecommends/SettingsUI/Art/03_按钮底板/Button_Secondary_Normal.png"),position,size);image.preserveAspect=false;image.raycastTarget=true;
            var button=image.GetComponent<Button>();if(!button)button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
            Label("Label",image.transform,content,Vector2.zero,size-new Vector2(10,2),fontSize);
            if(!image.GetComponent<UiClickSound>())image.gameObject.AddComponent<UiClickSound>();return button;
        }
        static void Box(RectTransform rect,Vector2 position,Vector2 size) { rect.anchoredPosition=position;rect.sizeDelta=size; }
    }
}
