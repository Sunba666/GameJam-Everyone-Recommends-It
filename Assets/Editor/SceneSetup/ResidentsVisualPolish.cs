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
    public static class ResidentsVisualPolish
    {
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before applying visual polish.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Residents.scene");
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Residents.scene",OpenSceneMode.Single);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_Visual";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            Attach(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Library/visual-polish-install.txt",DateTime.Now.ToString("O")+"\nReceipt arrows, larger props and bounded UI installed.\nBackup="+backup);
        }
        public static void Attach(ResidentsView view)
        {
            var art=view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Artboard_1672x941");
            SingleBackdropInstaller.Attach(view);
            if(!view.delivery)
            {
                var root=Rect("RecommendationDelivery",art,Vector2.zero,new Vector2(1672,941));
                var delivery=root.gameObject.AddComponent<RecommendationDeliveryView>();delivery.owner=view;
                var arrow=Rect("ThemeDeliveryArrow",root,Vector2.zero,new Vector2(1672,941)).gameObject.AddComponent<DeliveryArrowGraphic>();
                arrow.raycastTarget=false;arrow.gameObject.SetActive(false);delivery.arrow=arrow;
                delivery.receiptIcons=new Image[view.residents.Length];delivery.receiptLabels=new Text[view.residents.Length];
                for(int i=0;i<view.residents.Length;i++)
                {
                    var badge=Rect("ReceivedTopic",view.residents[i].transform,new Vector2(74,58),new Vector2(44,44));
                    var image=badge.gameObject.AddComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;
                    var outline=badge.gameObject.AddComponent<Outline>();outline.effectColor=new Color(1,.97f,.90f);outline.effectDistance=new Vector2(2,-2);
                    delivery.receiptIcons[i]=image;
                    var label=Label("ReceiptSource",badge,"收到推荐",new Vector2(0,-31),new Vector2(78,21),13);
                    var shadow=label.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(1,.97f,.90f);shadow.effectDistance=new Vector2(1,-1);
                    delivery.receiptLabels[i]=label;badge.gameObject.SetActive(false);
                }
                view.delivery=delivery;
            }
            // Render above the diagram, while observation cards and modal panels retain priority.
            view.delivery.transform.SetSiblingIndex(view.statusIcons[0].transform.GetSiblingIndex());view.statusCard.transform.SetAsLastSibling();
            foreach(var badge in view.delivery.receiptIcons)Box(badge.rectTransform,new Vector2(74,58),new Vector2(44,44));
            float[] propSize={128,116,68,68,76};
            for(int i=0;i<view.propImages.Length;i++)
            {
                view.propImages[i].rectTransform.sizeDelta=Vector2.one*propSize[i%5];
                view.propImages[i].preserveAspect=true;view.propImages[i].raycastTarget=false;
            }
            for(int i=0;i<view.topics.Length;i++)
            {
                var label=view.topics[i].GetComponentInChildren<Text>();
                Box(label.rectTransform,new Vector2(0,-21),new Vector2(110,24));Fit(label,20,18);
                var icon=view.topics[i].transform.Find("Icon") as RectTransform;Box(icon,new Vector2(0,14),new Vector2(38,38));
            }
            Box((RectTransform)view.fastForward.transform,new Vector2(286,350),new Vector2(194,40));
            Fit(view.fastForward.GetComponentInChildren<Text>(),20,18);
            Box(view.fastForward.GetComponentInChildren<Text>().rectTransform,Vector2.zero,new Vector2(174,32));
            var card=view.statusCard;Box((RectTransform)card.transform,Vector2.zero,new Vector2(260,340));
            Box(card.number.rectTransform,new Vector2(-18,130),new Vector2(184,30));Fit(card.number,20,18);
            Box((RectTransform)card.close.transform,new Vector2(105,130),new Vector2(30,30));
            Box(card.portrait.rectTransform,new Vector2(-72,84),new Vector2(62,62));
            Box(card.preference.rectTransform,new Vector2(70,84),new Vector2(46,46));
            Box(card.details.rectTransform,new Vector2(0,-18),new Vector2(230,140));
            card.details.fontSize=15;card.details.resizeTextForBestFit=true;card.details.resizeTextMinSize=14;card.details.resizeTextMaxSize=15;
            card.details.horizontalOverflow=HorizontalWrapMode.Wrap;card.details.verticalOverflow=VerticalWrapMode.Truncate;
            Box((RectTransform)card.confirm.transform,new Vector2(0,-112),new Vector2(230,32));
            Box(card.confirmLabel.rectTransform,Vector2.zero,new Vector2(208,28));Fit(card.confirmLabel,19,17);
            Box((RectTransform)card.decline.transform,new Vector2(0,-147),new Vector2(230,28));
            var decline=card.decline.GetComponentInChildren<Text>();Box(decline.rectTransform,Vector2.zero,new Vector2(208,24));Fit(decline,18,17);
            foreach(var response in view.reactionLabels)
            {
                Box(response.rectTransform,new Vector2(-26,80),new Vector2(158,32));Fit(response,14,12);
                response.color=new Color(.13f,.10f,.07f);
                if(!response.GetComponent<Shadow>()) { var shadow=response.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(1,.97f,.90f);shadow.effectDistance=new Vector2(1,-1); }
            }
            foreach(var button in new[]{view.allFloors,view.returnToMenu,view.settingsButton,view.pauseButton,view.upFloor,view.downFloor,view.changeNotice})
            {
                var label=button.GetComponentInChildren<Text>(true);if(label)Fit(label,Mathf.Min(label.fontSize,24),18);
            }
            view.feedback.resizeTextForBestFit=true;view.feedback.resizeTextMinSize=18;view.feedback.resizeTextMaxSize=24;
            view.feedback.horizontalOverflow=HorizontalWrapMode.Wrap;view.feedback.verticalOverflow=VerticalWrapMode.Truncate;
            var overlay=view.overlay;
            if(!overlay.reading)
            {
                var viewport=Rect("ReadingViewport",overlay.paper,new Vector2(0,45),new Vector2(808,380));
                var surface=viewport.gameObject.AddComponent<Image>();surface.color=Color.clear;surface.raycastTarget=true;
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
                scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
                overlay.body.rectTransform.SetParent(viewport,false);overlay.body.raycastTarget=true;
                scroll.viewport=viewport;scroll.content=overlay.body.rectTransform;
                overlay.reading=scroll;overlay.readingViewport=viewport;
                overlay.scrollHint=Label("ScrollHint",overlay.paper,"",new Vector2(0,-169),new Vector2(808,26),16);
                overlay.scrollHint.color=new Color(.45f,.40f,.34f);
            }
            overlay.heading.resizeTextForBestFit=true;overlay.heading.resizeTextMinSize=22;overlay.heading.resizeTextMaxSize=36;
            overlay.heading.horizontalOverflow=HorizontalWrapMode.Wrap;overlay.heading.verticalOverflow=VerticalWrapMode.Truncate;
            overlay.Layout(false);
            EditorUtility.SetDirty(card);EditorUtility.SetDirty(overlay);EditorUtility.SetDirty(view);
            if(view.paceButton)PlayerExperienceInstaller.Attach(view);
            UIPolishInstaller.AttachResidents(view);
        }
        public static void AttachWholeBackgroundLighting(ResidentsView view)
        {
            SingleBackdropInstaller.Attach(view);
        }
        public static void ApplyWholeBackgroundLighting()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before correcting background lighting.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Residents.scene");
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Residents.scene",OpenSceneMode.Single);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_Background";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            AttachWholeBackgroundLighting(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Library/background-fix.txt",DateTime.Now.ToString("O")+"\nOne full-building lighting layer; no middle-only tint.\nBackup="+backup);
        }
        static void Fit(Text text,int max,int min)
        {
            text.alignment=TextAnchor.MiddleCenter;text.fontSize=max;text.resizeTextForBestFit=true;text.resizeTextMinSize=min;text.resizeTextMaxSize=max;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        }
        static void Box(RectTransform rect,Vector2 position,Vector2 size) { rect.anchoredPosition=position;rect.sizeDelta=size; }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));go.layer=5;
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);Box(rect,pos,size);return rect;
        }
        static Text Label(string name,Transform parent,string value,Vector2 pos,Vector2 size,int font)
        {
            var label=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();label.font=Font;label.text=value;label.fontSize=font;
            label.color=new Color(.1f,.09f,.08f);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
        }
    }
}
