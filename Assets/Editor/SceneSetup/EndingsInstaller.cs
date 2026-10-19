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
    public static class EndingsInstaller
    {
        const string Art="Assets/EveryoneRecommends/AchievementsUI/Art/";
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        [MenuItem("Everyone Recommends/Install Six Endings And Achievements")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing endings.");
            string previous=SceneManager.GetActiveScene().path;
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_Endings";Directory.CreateDirectory(backup);
            var catalog=Catalog();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Residents.scene",OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var residents=UnityEngine.Object.FindObjectOfType<ResidentsView>();residents.endings=catalog;EditorUtility.SetDirty(residents);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.scene",OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene,backup+"/MainMenu.scene",true);
            var menu=UnityEngine.Object.FindObjectOfType<MenuView>();var root=menu.achievementsPanel;
            var view=root.GetComponent<AchievementsView>()??root.AddComponent<AchievementsView>();view.catalog=catalog;view.cards=new AchievementCardView[6];
            var panel=root.transform.Find("Panel");
            view.progress=Label("EndingProgress",panel,Vector2.up*252,new Vector2(850,28),20);
            view.details=Label("EndingDetails",panel,new Vector2(0,-278),new Vector2(1010,40),17);
            for(int i=0;i<6;i++)
            {
                var rect=root.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="AchievementSlot_"+(i+1).ToString("00"));
                rect.sizeDelta=new Vector2(320,220);
                var locked=rect.Find("Label_Locked");if(locked)locked.gameObject.SetActive(false);
                var image=rect.GetComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"03_成就卡片/Card_Blank.png");image.type=Image.Type.Sliced;image.raycastTarget=true;
                var card=rect.GetComponent<AchievementCardView>()??rect.gameObject.AddComponent<AchievementCardView>();
                card.id=(EndingId)i;card.button=rect.GetComponent<Button>()??rect.gameObject.AddComponent<Button>();card.button.targetGraphic=image;
                card.button.navigation=new Navigation{mode=Navigation.Mode.None};
                if(!rect.GetComponent<UiClickSound>())rect.gameObject.AddComponent<UiClickSound>();
                card.selected=rect.GetComponent<Outline>()??rect.gameObject.AddComponent<Outline>();card.selected.effectDistance=new Vector2(2,2);card.selected.effectColor=new Color(.95f,.60f,.40f);card.selected.enabled=false;
                card.icon=Icon("EndingIcon",rect,new Vector2(0,64),new Vector2(64,64));card.balanceIcons=new Image[5];
                for(int topic=0;topic<5;topic++)card.balanceIcons[topic]=Icon("BalanceIcon_"+topic,rect,new Vector2((topic-2)*42,64),Vector2.one*35);
                card.title=Label("EndingTitle",rect,new Vector2(0,10),new Vector2(282,32),24);
                card.condition=Label("EndingCondition",rect,new Vector2(0,-35),new Vector2(282,44),17);
                card.status=Label("EndingStatus",rect,new Vector2(0,-82),new Vector2(282,24),17);
                card.title.text=catalog.Get(card.id).title;card.condition.text=catalog.Get(card.id).Condition;card.status.text="未解锁";
                card.icon.sprite=i<5?catalog.topicIcons[i]:null;card.icon.gameObject.SetActive(i<5);
                for(int t=0;t<5;t++) { card.balanceIcons[t].sprite=catalog.topicIcons[t];card.balanceIcons[t].gameObject.SetActive(i==5); }
                view.cards[i]=card;EditorUtility.SetDirty(card);
            }
            UIPolishInstaller.AttachMenu(menu);EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous)?"Assets/Scenes/Residents.scene":previous,OpenSceneMode.Single);
            File.WriteAllText("Library/endings-install.txt",DateTime.Now.ToString("O")+"\nSix endings and persistent achievement cards installed.\nBackup="+backup);
        }
        static EndingCatalog Catalog()
        {
            Directory.CreateDirectory("Assets/Resources/Endings");string path="Assets/Resources/Endings/EndingCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<EndingCatalog>(path);
            if(!catalog)
            {
                catalog=ScriptableObject.CreateInstance<EndingCatalog>();
                string[] titles={"猫咪当家","美食当道","再来一局","全楼头条","同一首歌","势均力敌"};
                Color[] colors={new Color(.72f,.28f,.20f),new Color(.55f,.34f,.06f),new Color(.22f,.33f,.62f),new Color(.14f,.43f,.41f),new Color(.46f,.26f,.62f),new Color(.28f,.40f,.32f)};
                catalog.endings=new EndingDefinition[6];
                for(int i=0;i<6;i++)catalog.endings[i]=new EndingDefinition{id=(EndingId)i,title=titles[i],color=colors[i],description=i==5?"最后一轮结束，最高人数并列，没有唯一榜首。大家保留了不同的选择。":"最后一轮结束，愿意继续接触「"+ResidentRun.TopicNames[i]+"」的住户最多。这是楼里最终留下的热门话题。"};
                string[] icons={"S01_猫","S02_美食","S03_游戏","S04_新闻","S05_音乐"};
                catalog.topicIcons=icons.Select(n=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ImportedGame/"+n+".png")).ToArray();
                AssetDatabase.CreateAsset(catalog,path);
            }
            return catalog;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var old=parent.Find(name);var rect=old?(RectTransform)old:new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();
            rect.gameObject.layer=5;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=pos;rect.sizeDelta=size;return rect;
        }
        static Text Label(string name,Transform parent,Vector2 pos,Vector2 size,int fontSize)
        {
            var rect=Rect(name,parent,pos,size);var text=rect.GetComponent<Text>()??rect.gameObject.AddComponent<Text>();
            text.font=Font;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.16f,.12f,.08f);text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        static Image Icon(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var rect=Rect(name,parent,pos,size);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;return image;
        }
    }
}
