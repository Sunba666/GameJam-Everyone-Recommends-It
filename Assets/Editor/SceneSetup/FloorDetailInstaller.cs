using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using EveryoneRecommends.Frontend;

namespace EveryoneRecommends.EditorTools
{
    public static class FloorDetailInstaller
    {
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        [MenuItem("Everyone Recommends/Install Detailed Floor Interiors")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing floor interiors.");
            var definitions=Definitions();
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Residents.scene");
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Residents.scene",OpenSceneMode.Single);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_FloorDetail";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            var art=owner.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Artboard_1672x941");
            var detail=owner.floorDetails;
            if(!detail)
            {
                detail=owner.gameObject.AddComponent<FloorDetailView>();detail.owner=owner;owner.floorDetails=detail;
                var panel=Rect("FloorInteriorPanel",art,new Vector2(-220,0),new Vector2(1228,941));
                panel.SetSiblingIndex(owner.buildingViewport.GetSiblingIndex()+1);detail.panel=panel.gameObject;
                detail.group=panel.gameObject.AddComponent<CanvasGroup>();detail.group.alpha=1;
                detail.paper=panel.gameObject.AddComponent<Image>();detail.paper.color=new Color(.94f,.89f,.80f);detail.paper.raycastTarget=false;
                detail.viewport=Rect("InteriorViewport",panel,new Vector2(0,-25),new Vector2(1228,691));detail.viewport.gameObject.AddComponent<RectMask2D>();
                detail.stage=Rect("InteriorStage",detail.viewport,Vector2.zero,new Vector2(1228,691));
                detail.dayImage=Image("InteriorDay",detail.stage);detail.nightImage=Image("InteriorNight",detail.stage);
                var caption=Rect("InteriorCaption",panel,new Vector2(0,322),new Vector2(700,30)).gameObject.AddComponent<Text>();
                caption.font=Font;caption.fontSize=20;caption.color=new Color(.20f,.15f,.10f);caption.alignment=TextAnchor.MiddleCenter;caption.raycastTarget=false;detail.caption=caption;
            }
            detail.caption.rectTransform.anchoredPosition=new Vector2(100,-440);
            detail.definitions=definitions;detail.anchorGroups=new GameObject[3];var anchors=new List<FloorInteractionAnchor>();
            for(int floor=1;floor<=3;floor++)
            {
                var name="InteractionAnchors_"+floor+"F";var existing=detail.stage.Find(name);
                var root=existing?(RectTransform)existing:Rect(name,detail.stage,Vector2.zero,new Vector2(1228,691));detail.anchorGroups[floor-1]=root.gameObject;
                foreach(var spec in definitions[floor-1].anchors)
                {
                    string room=definitions[floor-1].Room(spec.residentInFloor);string id=room+"/"+spec.kind;
                    var old=root.Find(room+"_"+spec.kind);var rect=old?(RectTransform)old:Rect(room+"_"+spec.kind,root,Vector2.zero,Vector2.zero);
                    rect.anchoredPosition=Vector2.Scale(spec.normalizedPosition-Vector2.one*.5f,detail.stage.rect.size);
                    rect.sizeDelta=Vector2.Scale(spec.normalizedSize,detail.stage.rect.size);
                    var anchor=rect.GetComponent<FloorInteractionAnchor>();if(!anchor)anchor=rect.gameObject.AddComponent<FloorInteractionAnchor>();
                    anchor.stableId=id;anchor.room=room;anchor.kind=spec.kind;anchors.Add(anchor);EditorUtility.SetDirty(anchor);
                }
                root.gameObject.SetActive(false);
            }
            detail.anchors=anchors.ToArray();detail.dayImage.sprite=definitions[0].day;detail.nightImage.sprite=definitions[0].night;detail.nightImage.color=new Color(1,1,1,0);detail.panel.SetActive(false);
            EditorUtility.SetDirty(detail);EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Library/floor-detail-install.txt",DateTime.Now.ToString("O")+"\nThree paired day/night interiors; stable room interaction anchors; original residents reused.\nBackup="+backup);
        }
        static FloorSceneDefinition[] Definitions()
        {
            var result=new FloorSceneDefinition[3];
            float[][] tableX={new[]{.115f,.40f,.64f},new[]{.105f,.58f,.89f},new[]{.10f,.375f,.90f}};
            for(int floor=1;floor<=3;floor++)
            {
                var sprites=new Sprite[2];int index=0;
                foreach(string light in new[]{"Day","Night"})
                {
                    string path="Assets/Art/Building/FloorDetails/Floor"+floor+"_Inside_"+light+"_v1.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                    sprites[index++]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                string asset="Assets/Resources/FloorDetails/Floor"+floor+".asset";var definition=AssetDatabase.LoadAssetAtPath<FloorSceneDefinition>(asset);
                bool fresh=!definition;if(fresh)definition=ScriptableObject.CreateInstance<FloorSceneDefinition>();
                definition.floor=floor;definition.day=sprites[0];definition.night=sprites[1];
                if(fresh)
                {
                    var specs=new List<FloorAnchorSpec>();
                    for(int resident=0;resident<3;resident++)
                    {
                        float x=definition.residents[resident].x;
                        specs.Add(Spec(resident,FloorAnchorKind.Seat,new Vector2(x,.42f),new Vector2(.16f,.25f)));
                        specs.Add(Spec(resident,FloorAnchorKind.Tabletop,new Vector2(tableX[floor-1][resident],.34f),new Vector2(.055f,.035f)));
                        specs.Add(Spec(resident,FloorAnchorKind.Floor,new Vector2(x,.22f),new Vector2(.12f,.075f)));
                        specs.Add(Spec(resident,FloorAnchorKind.Wall,new Vector2(x,.72f),new Vector2(.12f,.14f)));
                    }
                    specs.Add(Spec(0,FloorAnchorKind.Window,new Vector2(.055f,.64f),new Vector2(.065f,.25f)));
                    definition.anchors=specs.ToArray();AssetDatabase.CreateAsset(definition,asset);
                }
                EditorUtility.SetDirty(definition);result[floor-1]=definition;
            }
            return result;
        }
        static FloorAnchorSpec Spec(int room,FloorAnchorKind kind,Vector2 pos,Vector2 size)=>new FloorAnchorSpec{residentInFloor=room,kind=kind,normalizedPosition=pos,normalizedSize=size};
        static Image Image(string name,Transform parent)
        {
            var image=Rect(name,parent,Vector2.zero,new Vector2(1228,691)).gameObject.AddComponent<Image>();image.raycastTarget=false;return image;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));go.layer=5;var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=pos;rect.sizeDelta=size;return rect;
        }
    }
}
