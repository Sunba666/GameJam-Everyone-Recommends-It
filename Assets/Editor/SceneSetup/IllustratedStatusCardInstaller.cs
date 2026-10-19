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
    // Source coordinates refer to the untouched 1120 x 1400 supplied card.
    public static class IllustratedStatusCardInstaller
    {
        const string CardPath="Assets/Art/UI/ResidentStatus/NPC状态卡_通用底板.png";
        const float Scale=340f/1120f;
        static readonly Color Ink=new Color(.09f,.08f,.10f);
        static readonly Color[] Colors={new Color(1,.41f,.29f),new Color(1,.73f,.25f),new Color(.60f,.71f,.96f),new Color(.68f,.85f,.83f),new Color(.79f,.68f,.96f)};
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        [MenuItem("Everyone Recommends/Install Illustrated Resident Card")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing the status card.");
            const string path="Assets/Scenes/Residents.scene";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_IllustratedCard";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            Attach(owner);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(opened)EditorSceneManager.CloseScene(scene,true);
            File.WriteAllText("Library/status-card-install.txt",DateTime.Now.ToString("O")+"\nIllustrated card with live pressure cells, individual thresholds and attitudes.\nBackup="+backup);
        }
        public static void Attach(ResidentsView owner)
        {
            var card=owner.statusCard;card.gameObject.SetActive(false);
            var root=(RectTransform)card.transform;root.sizeDelta=new Vector2(356,548);
            var art=card.placementArea;
            var background=card.GetComponent<Image>();background.sprite=null;background.type=UnityEngine.UI.Image.Type.Simple;
            background.color=art.Find("LiveStatusPanel").GetComponent<Image>().color;background.raycastTarget=true;
            var plate=Image("IllustratedPlate",root);Box(plate.rectTransform,new Vector2(0,58),new Vector2(340,425));
            plate.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(CardPath);plate.color=Color.white;plate.preserveAspect=true;plate.transform.SetAsFirstSibling();
            var source=plate.rectTransform;
            Position(card.portrait.rectTransform,source,300,363,390,300);
            Position(card.preference.rectTransform,source,793,430,240,230);
            Position(card.number.rectTransform,source,300,535,138,80);Style(card.number,15);
            Position((RectTransform)card.close.transform,source,942,134,150,150);
            var hit=card.close.GetComponent<Image>();hit.sprite=null;hit.color=Color.clear;hit.raycastTarget=true;
            card.close.targetGraphic=hit;card.close.transition=Selectable.Transition.None;
            foreach(var graphic in card.close.GetComponentsInChildren<Graphic>(true))if(graphic!=hit)graphic.gameObject.SetActive(false);
            var closeMotion=card.close.GetComponent<UiButtonFeedback>();if(closeMotion)closeMotion.enabled=false;
            card.cells=new Image[50];card.attitudes=new AttitudeFaceGraphic[5];card.thresholdMarkers=new RectTransform[5];card.overloadMarks=new Text[5];
            float[] rows={716,831,950,1070,1189};float[] columns={322,376,432,489,544,601,658,716,774,827};
            for(int topic=0;topic<5;topic++)
            {
                for(int cell=0;cell<10;cell++)
                {
                    var fillRect=Rect("Saturation_"+topic+"_"+cell,source);
                    var fill=fillRect.GetComponent<PressureCellGraphic>();
                    if(!fill) { var old=fillRect.GetComponent<Image>();if(old)UnityEngine.Object.DestroyImmediate(old);fill=fillRect.gameObject.AddComponent<PressureCellGraphic>(); }
                    Position(fill.rectTransform,source,columns[cell],rows[topic],cell==0||cell==9?36:44,46);
                    fill.color=Colors[topic];fill.sprite=null;fill.raycastTarget=false;
                    fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=0;
                    card.cells[topic*10+cell]=fill;
                }
                // Cream outline keeps a threshold visible over both ink separators and filled cells.
                var halo=Image("Threshold_"+topic,source);Position(halo.rectTransform,source,687,rows[topic],20,82);halo.color=new Color(1,.9647f,.8824f);
                var marker=Image("Ink",halo.transform);Box(marker.rectTransform,Vector2.zero,new Vector2(10,76)*Scale);marker.color=Ink;
                card.thresholdMarkers[topic]=halo.rectTransform;
                var faceRect=Rect("Attitude_"+topic,source);Position(faceRect,source,948,rows[topic],76,76);
                var face=faceRect.GetComponent<AttitudeFaceGraphic>()??faceRect.gameObject.AddComponent<AttitudeFaceGraphic>();face.raycastTarget=false;card.attitudes[topic]=face;
                var warning=Label("Overloaded_"+topic,source,19);Position(warning.rectTransform,source,1026,rows[topic],50,80);warning.text="!";warning.color=Colors[0];card.overloadMarks[topic]=warning;warning.gameObject.SetActive(false);
            }
            card.thresholdRange=new Vector2((302-560)*Scale,(847-560)*Scale);
            card.pauseNote=Label("ObservationPauseNote",root,15);Box(card.pauseNote.rectTransform,new Vector2(0,-162),new Vector2(340,20));card.pauseNote.color=new Color(.22f,.44f,.33f);
            Box(card.details.rectTransform,new Vector2(0,-191),new Vector2(332,40));Style(card.details,17);card.details.alignment=TextAnchor.UpperLeft;
            card.recent=Label("RecentContacts",root,14);Box(card.recent.rectTransform,new Vector2(0,-220),new Vector2(332,20));card.recent.color=new Color(.36f,.31f,.25f);
            Box((RectTransform)card.confirm.transform,new Vector2(-66,-252),new Vector2(190,36));Box(card.confirmLabel.rectTransform,Vector2.zero,new Vector2(178,32));Style(card.confirmLabel,19);
            Box((RectTransform)card.decline.transform,new Vector2(105,-252),new Vector2(128,36));var decline=card.decline.GetComponentInChildren<Text>(true);Box(decline.rectTransform,Vector2.zero,new Vector2(118,32));Style(decline,18);
            card.sidebarDock=Rect("ObservationDock",art);Box(card.sidebarDock,new Vector2(620,40),root.sizeDelta);
            card.coveredSidebar=owner.statusIcons.Select(i=>i.gameObject).Concat(owner.floors.Select(b=>b.gameObject)).Concat(new[]{art.Find("Prompt").gameObject,owner.selectionLabel.gameObject}).ToArray();
            card.illustrated=true;root.SetAsLastSibling();EditorUtility.SetDirty(card);EditorUtility.SetDirty(owner);
        }
        static RectTransform Rect(string name,Transform parent)
        {
            var old=parent.Find(name);var rect=old?(RectTransform)old:new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.gameObject.layer=5;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.localScale=Vector3.one;return rect;
        }
        static Image Image(string name,Transform parent)
        {
            var rect=Rect(name,parent);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();image.raycastTarget=false;return image;
        }
        static Text Label(string name,Transform parent,int size)
        {
            var rect=Rect(name,parent);var label=rect.GetComponent<Text>()??rect.gameObject.AddComponent<Text>();Style(label,size);return label;
        }
        static void Style(Text text,int size)
        {
            text.font=Font;text.fontSize=size;text.fontStyle=FontStyle.Normal;text.resizeTextForBestFit=false;text.alignment=TextAnchor.MiddleCenter;text.color=Ink;text.raycastTarget=false;text.lineSpacing=1;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        }
        static void Position(RectTransform rect,Transform parent,float x,float y,float width,float height)
        {
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.localScale=Vector3.one;
            Box(rect,new Vector2(x-560,700-y)*Scale,new Vector2(width,height)*Scale);
        }
        static void Box(RectTransform rect,Vector2 position,Vector2 size) { rect.anchoredPosition=position;rect.sizeDelta=size; }
    }
}
