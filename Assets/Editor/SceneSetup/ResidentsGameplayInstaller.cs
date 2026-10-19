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
    public static class ResidentsGameplayInstaller
    {
        const string ScenePath="Assets/Scenes/Residents.scene";
        const string CardPath="Assets/Art/UI/ResidentStatus/NPC状态卡_通用底板.png";
        const string UiArt="Assets/EveryoneRecommends/SettingsUI/Art/";
        static readonly Color Ink=new Color(.094f,.086f,.106f);
        static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");

        [MenuItem("Everyone Recommends/Add Resident Status And Stage Timer")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing gameplay UI.");
            string backup="Design/ResidentsStatusBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup); File.Copy(ScenePath,backup+"/Residents.scene");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var current=SceneManager.GetSceneAt(i);
                if(current.isDirty)
                {
                    EditorSceneManager.SaveScene(current,backup+"/"+current.name+"_Unsaved.scene",true);
                    EditorSceneManager.SaveScene(current);
                }
            }
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid() || !scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            AssetDatabase.ImportAsset(CardPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(CardPath);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;
            importer.filterMode=FilterMode.Bilinear; importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
            var textureSettings=new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            Attach(view);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("Library/residents-gameplay-install.txt",DateTime.Now.ToString("O")+"\n10 resident cards wired; 90 second / 12 stage UI saved.\nBackup="+backup);
            Debug.Log("[Residents] Status cards and stage timer installed; existing positions preserved.");
        }

        public static void Attach(ResidentsView view)
        {
            if(view.overlay) { DemoSceneInstaller.Attach(view);return; }
            var art=view.residents[0].transform.parent as RectTransform;
            for(int i=0;i<view.residents.Length;i++)
            {
                MakeClickable(view.residents[i]); MakeClickable(view.statusIcons[i]);
                var outline=view.residents[i].GetComponent<Outline>();
                if(!outline)outline=view.residents[i].gameObject.AddComponent<Outline>();
                outline.effectColor=new Color(1,.5f,.32f); outline.effectDistance=new Vector2(2,-2); outline.enabled=false;
                EditorUtility.SetDirty(outline);
            }
            if(!view.stageProgress)view.stageProgress=CreateTimer(view,art);
            if(!view.statusCard)view.statusCard=CreateCard(view,art);
            foreach(var face in view.statusCard.attitudes)
                if(!face.GetComponent<CanvasRenderer>())face.gameObject.AddComponent<CanvasRenderer>();
            view.stageProgress.stageLabel.rectTransform.anchoredPosition=new Vector2(-290,12);
            view.stageProgress.timeLabel.rectTransform.anchoredPosition=new Vector2(302,12);
            view.stageProgress.hint.rectTransform.anchoredPosition=new Vector2(0,12);
            var nextRect=(RectTransform)view.stageProgress.next.transform;
            nextRect.anchoredPosition=new Vector2(15,13); nextRect.sizeDelta=new Vector2(236,30);
            view.stageProgress.nextLabel.rectTransform.sizeDelta=new Vector2(220,26);
            view.statusCard.gameObject.SetActive(false);
            EditorUtility.SetDirty(view);
        }

        static void MakeClickable(Image image)
        {
            image.raycastTarget=true;
            var button=image.GetComponent<Button>(); if(!button)button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image; button.transition=Selectable.Transition.None;
            if(!image.GetComponent<UiClickSound>())image.gameObject.AddComponent<UiClickSound>();
            EditorUtility.SetDirty(image); EditorUtility.SetDirty(button);
        }
        static StageProgressView CreateTimer(ResidentsView view,RectTransform art)
        {
            var root=Rect("StageProgress",art,new Vector2(-25,422),new Vector2(800,76));
            var plate=root.gameObject.AddComponent<Image>(); plate.sprite=Sprite(UiArt+"03_按钮底板/Button_Secondary_Normal.png"); plate.raycastTarget=false;
            var hud=root.gameObject.AddComponent<StageProgressView>(); hud.owner=view;
            hud.stageLabel=Label("Stage",root,"阶段 1 / 12",new Vector2(-306,18),new Vector2(166,30),23);
            hud.timeLabel=Label("Remaining",root,"90 秒",new Vector2(318,18),new Vector2(136,30),24);
            hud.hint=Label("Hint",root,"观察住户 · 推荐内容",new Vector2(0,18),new Vector2(390,30),20);
            var track=Picture("Track",root,Sprite(UiArt+"04_滑条/Slider_Track_Normal.png"),new Vector2(0,-17),new Vector2(752,30));
            hud.fill=Picture("TimeFill",track.transform,Sprite(UiArt+"04_滑条/Slider_Fill_Normal.png"),Vector2.zero,new Vector2(736,20));
            hud.fill.type=Image.Type.Filled; hud.fill.fillMethod=Image.FillMethod.Horizontal;
            hud.fill.fillOrigin=(int)Image.OriginHorizontal.Left; hud.fill.fillAmount=1;
            hud.cursor=Picture("TimeCursor",track.transform,Sprite(UiArt+"04_滑条/Slider_Knob_Normal.png"),new Vector2(368,0),new Vector2(30,30)).rectTransform;
            hud.next=Button("NextStage",root,"下一阶段 →",new Vector2(15,18),new Vector2(236,34),20);
            hud.nextLabel=hud.next.GetComponentInChildren<Text>(); hud.next.gameObject.SetActive(false);
            return hud;
        }
        static ResidentStatusCard CreateCard(ResidentsView view,RectTransform art)
        {
            var root=Rect("ResidentStatusCard",art,Vector2.zero,new Vector2(260,325));
            var background=root.gameObject.AddComponent<Image>(); background.sprite=Sprite(CardPath); background.raycastTarget=true;
            var card=root.gameObject.AddComponent<ResidentStatusCard>(); card.owner=view; card.placementArea=art;
            var livePanel=art.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="LiveStatusPanel");
            card.reservedAreas=new[]{livePanel,(RectTransform)view.stageProgress.transform,view.feedback.rectTransform,(RectTransform)view.allFloors.transform};
            card.portrait=SourcePicture("Portrait",root,null,300,370,292,244); card.portrait.preserveAspect=true;
            card.preference=SourcePicture("Preference",root,null,793,430,282,242); card.preference.preserveAspect=true;
            var numberRect=SourceRect("Number",root,300,535,150,60);
            card.number=numberRect.gameObject.AddComponent<Text>(); TextStyle(card.number,13); card.number.text="01";
            var close=SourcePicture("CloseHitArea",root,null,946,133,164,150); close.color=Color.clear; close.raycastTarget=true;
            card.close=close.gameObject.AddComponent<Button>(); card.close.targetGraphic=close; card.close.transition=Selectable.Transition.None;
            close.gameObject.AddComponent<UiClickSound>();
            card.cells=new Image[50]; card.attitudes=new AttitudeFaceGraphic[5];
            float[] rows={716,831,950,1070,1189}; float[] columns={322,376,432,489,544,601,658,716,774,827};
            for(int topic=0;topic<5;topic++)
            {
                for(int cell=0;cell<10;cell++)
                {
                    var image=SourcePicture("Saturation_"+topic+"_"+cell,root,null,columns[cell],rows[topic],cell==0||cell==9?38:44,46);
                    image.enabled=false; card.cells[topic*10+cell]=image;
                }
                var marker=SourcePicture("Threshold_"+topic,root,null,687,rows[topic],10,78); marker.color=Ink;
                var face=SourceRect("Attitude_"+topic,root,948,rows[topic],76,76).gameObject.AddComponent<AttitudeFaceGraphic>();
                face.raycastTarget=false; card.attitudes[topic]=face;
            }
            return card;
        }
        static RectTransform SourceRect(string name,Transform parent,float x,float y,float width,float height)
        {
            const float scale=260f/1120;
            return Rect(name,parent,new Vector2((x-560)*scale,(700-y)*scale),new Vector2(width,height)*scale);
        }
        static Image SourcePicture(string name,Transform parent,Sprite sprite,float x,float y,float width,float height)
        {
            var image=SourceRect(name,parent,x,y,width,height).gameObject.AddComponent<Image>();
            image.sprite=sprite; image.raycastTarget=false; return image;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)); obj.layer=5;
            var rect=obj.GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        static Image Picture(string name,Transform parent,Sprite sprite,Vector2 position,Vector2 size)
        {
            var image=Rect(name,parent,position,size).gameObject.AddComponent<Image>(); image.sprite=sprite; image.raycastTarget=false; return image;
        }
        static Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,int fontSize)
        {
            var label=Rect(name,parent,position,size).gameObject.AddComponent<Text>(); TextStyle(label,fontSize); label.text=text; return label;
        }
        static void TextStyle(Text text,int size)
        {
            text.font=Font; text.fontSize=size; text.fontStyle=FontStyle.Bold; text.color=Ink;
            text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false;
        }
        static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size,int fontSize)
        {
            var image=Picture(name,parent,Sprite(UiArt+"03_按钮底板/Button_Secondary_Normal.png"),position,size); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=Sprite(UiArt+"03_按钮底板/Button_Secondary_Hover.png"),pressedSprite=Sprite(UiArt+"03_按钮底板/Button_Secondary_Pressed.png"),disabledSprite=Sprite(UiArt+"03_按钮底板/Button_Secondary_Disabled.png")};
            Label("Label",image.transform,text,Vector2.zero,size-new Vector2(16,4),fontSize); image.gameObject.AddComponent<UiClickSound>(); return button;
        }
        static Sprite Sprite(string path)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(!sprite)throw new InvalidOperationException("Missing gameplay sprite: "+path);
            return sprite;
        }
    }
}
