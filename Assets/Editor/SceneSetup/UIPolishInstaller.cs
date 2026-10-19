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
    public static class UIPolishInstaller
    {
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        static UIFeedbackProfile Profile()
        {
            const string path="Assets/Resources/UIFeedback/Default.asset";
            var profile=AssetDatabase.LoadAssetAtPath<UIFeedbackProfile>(path);
            if(!profile) { Directory.CreateDirectory("Assets/Resources/UIFeedback");profile=ScriptableObject.CreateInstance<UIFeedbackProfile>();AssetDatabase.CreateAsset(profile,path); }
            return profile;
        }
        [MenuItem("Everyone Recommends/Polish UI And Motion")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before polishing UI.");
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_UIPolish";Directory.CreateDirectory(backup);
            foreach(string path in new[]{"Assets/Scenes/Residents.scene","Assets/Scenes/MainMenu.scene"})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                EditorSceneManager.SaveScene(scene,backup+"/"+scene.name+".scene",true);
                var roots=scene.GetRootGameObjects();
                foreach(var owner in roots.SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)))AttachResidents(owner);
                foreach(var menu in roots.SelectMany(g=>g.GetComponentsInChildren<MenuView>(true)))AttachMenu(menu);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                if(opened)EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.SaveAssets();File.WriteAllText("Library/ui-polish-install.txt",DateTime.Now.ToString("O")+"\nReadable cards, reaction paper, content-only button motion and shared profile installed.\nBackup="+backup);
        }
        public static void AttachMenu(MenuView menu) { AttachButtons(menu.gameObject,Profile()); }
        public static void AttachResidents(ResidentsView owner)
        {
            ScatteredSharingInstaller.Attach(owner);
            var profile=Profile();var card=owner.statusCard;var root=(RectTransform)card.transform;root.sizeDelta=profile.cardSize;
            Box(card.number.rectTransform,new Vector2(-20,169),new Vector2(218,30));FontSize(card.number,23);
            Box((RectTransform)card.close.transform,new Vector2(116,165),new Vector2(34,34));
            Box(card.portrait.rectTransform,new Vector2(-80,120),new Vector2(66,66));Box(card.preference.rectTransform,new Vector2(80,120),new Vector2(48,48));
            card.pauseNote=Label("ObservationPauseNote",root,new Vector2(0,77),new Vector2(264,22),15);card.pauseNote.color=new Color(.22f,.44f,.33f);
            Box(card.details.rectTransform,new Vector2(0,-6),new Vector2(264,136));FontSize(card.details,profile.cardFontSize);card.details.lineSpacing=1;card.details.alignment=TextAnchor.UpperLeft;
            card.recent=Label("RecentContacts",root,new Vector2(0,-94),new Vector2(264,42),15);card.recent.alignment=TextAnchor.UpperLeft;card.recent.color=new Color(.36f,.31f,.25f);
            Box((RectTransform)card.confirm.transform,new Vector2(0,-143),new Vector2(264,36));Box(card.confirmLabel.rectTransform,Vector2.zero,new Vector2(238,32));FontSize(card.confirmLabel,20);
            Box((RectTransform)card.decline.transform,new Vector2(0,-178),new Vector2(264,30));FontSize(card.decline.GetComponentInChildren<Text>(),18);
            owner.reactionBubbles=new ResidentReactionBubble[owner.residents.Length];
            var paperSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EveryoneRecommends/AchievementsUI/Art/03_成就卡片/Card_Blank.png");
            for(int i=0;i<owner.residents.Length;i++)
            {
                var actor=owner.residents[i];var bubble=actor.GetComponent<ResidentReactionBubble>()??actor.gameObject.AddComponent<ResidentReactionBubble>();
                var rect=Rect("ReactionPaper",actor.transform,new Vector2(-26,87),new Vector2(154,46));
                var paper=rect.GetComponent<Image>();if(!paper)paper=rect.gameObject.AddComponent<Image>();paper.sprite=paperSprite;paper.type=Image.Type.Sliced;paper.pixelsPerUnitMultiplier=6;paper.raycastTarget=false;
                bubble.owner=owner;bubble.profile=profile;bubble.paper=rect;bubble.group=rect.GetComponent<CanvasGroup>();if(!bubble.group)bubble.group=rect.gameObject.AddComponent<CanvasGroup>();bubble.label=owner.reactionLabels[i];
                bubble.label.rectTransform.SetParent(rect,false);Box(bubble.label.rectTransform,Vector2.zero,new Vector2(136,34));FontSize(bubble.label,14);bubble.label.alignment=TextAnchor.MiddleCenter;bubble.label.raycastTarget=false;
                var shadow=bubble.label.GetComponent<Shadow>();if(shadow)shadow.enabled=false;
                rect.SetAsLastSibling();rect.gameObject.SetActive(false);owner.reactionBubbles[i]=bubble;EditorUtility.SetDirty(bubble);
            }
            FontSize(owner.selectionLabel,23);owner.selectionLabel.rectTransform.sizeDelta=new Vector2(420,62);
            IllustratedStatusCardInstaller.Attach(owner);
            CreativeBeatsInstaller.Attach(owner);
            AttachButtons(owner.gameObject,profile);EditorUtility.SetDirty(card);EditorUtility.SetDirty(owner);
        }
        static void AttachButtons(GameObject root,UIFeedbackProfile profile)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                // Resident actors have their own animator; do not alter any actor transform.
                if(button.GetComponent<ResidentMotion>()||button.name.StartsWith("Status_"))continue;
                var visuals=button.transform.Cast<Transform>().Where(t=>t.GetComponent<Graphic>()&&!t.name.StartsWith("Reaction")).Select(t=>t as RectTransform).Where(t=>t).ToArray();
                if(visuals.Length==0)
                {
                    // Some supplied title buttons bake their lettering into the root image.
                    // Keep a transparent raycast image on the root and animate a child copy.
                    var hit=button.GetComponent<Image>();
                    if(hit&&hit.sprite)
                    {
                        var artRect=Rect("ButtonArtwork",button.transform,Vector2.zero,((RectTransform)button.transform).sizeDelta);
                        var art=artRect.GetComponent<Image>();if(!art)art=artRect.gameObject.AddComponent<Image>();
                        art.sprite=hit.sprite;art.type=hit.type;art.preserveAspect=hit.preserveAspect;art.pixelsPerUnitMultiplier=hit.pixelsPerUnitMultiplier;art.color=Color.white;art.raycastTarget=false;
                        hit.sprite=null;hit.color=Color.clear;hit.raycastTarget=true;button.targetGraphic=art;visuals=new[]{artRect};
                    }
                }
                if(visuals.Length==0)continue;
                var feedback=button.GetComponent<UiButtonFeedback>()??button.gameObject.AddComponent<UiButtonFeedback>();feedback.profile=profile;feedback.visuals=visuals;EditorUtility.SetDirty(feedback);
            }
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var old=parent.Find(name);var rect=old?(RectTransform)old:new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();
            rect.gameObject.layer=5;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;Box(rect,pos,size);return rect;
        }
        static Text Label(string name,Transform parent,Vector2 pos,Vector2 size,int fontSize)
        {
            var rect=Rect(name,parent,pos,size);var text=rect.GetComponent<Text>()??rect.gameObject.AddComponent<Text>();text.font=Font;FontSize(text,fontSize);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        static void FontSize(Text text,int size) { text.fontSize=size;text.resizeTextForBestFit=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate; }
        static void Box(RectTransform rect,Vector2 pos,Vector2 size) { rect.anchoredPosition=pos;rect.sizeDelta=size; }
    }
}
