using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using EveryoneRecommends.Frontend;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EveryoneRecommends.EditorTools
{
    public static class StatePropMotionInstaller
    {
        const string Root="Assets/EveryoneRecommends/ResidentMotion/Props";
        const string Folder="Builds/Verification/StatePropMotion";
        static readonly string[] Ids={"CatSitting","CatLying","MusicNotes"};
        [Serializable] sealed class RectRecord
        {
            public Vector2 anchorsMin,anchorsMax,position,size,pivot;
            public Vector3 scale,rotation;
            public RectRecord(RectTransform rect)
            { anchorsMin=rect.anchorMin;anchorsMax=rect.anchorMax;position=rect.anchoredPosition;size=rect.sizeDelta;pivot=rect.pivot;scale=rect.localScale;rotation=rect.localEulerAngles; }
        }
        static string Geometry(IEnumerable<RectTransform> rects)=>string.Join("\n",rects.Select(rect=>JsonUtility.ToJson(new RectRecord(rect))));
        static string Hash(string path)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
        }
        [MenuItem("Everyone Recommends/Install State Prop Motion")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing activity companions.");
            const string scenePath="Assets/Scenes/Residents.scene",menuPath="Assets/Scenes/MainMenu.scene";
            string menuHash=Hash(menuPath),metaHash=Hash(scenePath+".meta");
            var scene=SceneManager.GetSceneByPath(scenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_StatePropMotionInstaller";
            Directory.CreateDirectory(backup);File.Copy(scenePath,backup+"/Residents.scene",true);
            if(scene.isDirty)EditorSceneManager.SaveScene(scene,backup+"/Residents_Unsaved.scene",true);
            var owner=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ResidentsView>(true)).Single();
            if(owner.residents.Length!=9||!owner.catCare||!owner.catCare.cat)throw new InvalidOperationException("Existing nine residents and cat-care presentation are required.");
            var originalRects=owner.residents.Select(image=>image.rectTransform).Concat(owner.GetComponentsInChildren<Button>(true).Select(button=>(RectTransform)button.transform)).Distinct().ToArray();
            string geometry=Geometry(originalRects);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var controllers=new RuntimeAnimatorController[3];var stills=new Sprite[3];var report=new List<string>();
            for(int action=0;action<Ids.Length;action++)
            {
                string id=Ids[action],path=Root+"/"+id;var frames=new Sprite[121];int maximum=action==2?128:256;
                for(int frame=0;frame<frames.Length;frame++)
                {
                    string image=path+"/Frames/"+id+"_"+frame.ToString("0000")+".png";
                    if(!File.Exists(image))throw new FileNotFoundException("Missing converted activity companion frame",image);
                    ImportFrame(image,maximum);frames[frame]=AssetDatabase.LoadAssetAtPath<Sprite>(image);
                    if(!frames[frame])throw new InvalidOperationException("Frame not imported as Sprite: "+image);
                }
                string clipPath=path+"/"+id+"_UI.anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(!clip) { clip=new AnimationClip{name=id+"_UI"};AssetDatabase.CreateAsset(clip,clipPath); }
                clip.ClearCurves();clip.frameRate=24;var keys=new ObjectReferenceKeyframe[121];
                for(int frame=0;frame<keys.Length;frame++)keys[frame]=new ObjectReferenceKeyframe{time=frame/24f,value=frames[frame]};
                AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(Image),propertyName="m_Sprite"},keys);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.startTime=0;settings.stopTime=121/24f;
                AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
                string controllerPath=path+"/"+id+"_UI.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;var state=machine.defaultState;if(!state)state=machine.AddState("Play");
                state.motion=clip;state.speed=1;machine.defaultState=state;EditorUtility.SetDirty(state);EditorUtility.SetDirty(machine);EditorUtility.SetDirty(controller);
                controllers[action]=controller;stills[action]=frames[action==2?30:0];report.Add("PASS "+id+": full121-frame/24fps sprite-only loop; max texture="+maximum+".");
            }
            var view=owner.GetComponent<StatePropMotionView>();if(!view)view=owner.gameObject.AddComponent<StatePropMotionView>();
            view.owner=owner;view.controllers=controllers;view.stills=stills;view.companions=new Image[9];view.companionAnimators=new Animator[9];
            view.lyingPosition=new Vector2(-75,-24);view.lyingSize=new Vector2(118,74);view.notesPosition=new Vector2(-72,55);view.notesSize=new Vector2(92,92);
            for(int index=0;index<owner.residents.Length;index++)
            {
                var actor=owner.residents[index];var child=actor.transform.Find("ActivityCompanion");
                if(!child)child=new GameObject("ActivityCompanion",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Animator)).transform;
                child.SetParent(actor.transform,false);var rect=(RectTransform)child;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=view.lyingPosition;rect.sizeDelta=view.lyingSize;rect.localScale=Vector3.one;rect.localEulerAngles=Vector3.zero;
                var image=child.GetComponent<Image>();image.sprite=stills[1];image.preserveAspect=true;image.raycastTarget=false;
                var animator=child.GetComponent<Animator>();Prepare(animator);animator.enabled=false;
                view.companions[index]=image;view.companionAnimators[index]=animator;child.gameObject.SetActive(false);
            }
            var care=owner.catCare.cat;view.careAnimator=care.GetComponent<Animator>();if(!view.careAnimator)view.careAnimator=care.gameObject.AddComponent<Animator>();
            Prepare(view.careAnimator);view.careAnimator.runtimeAnimatorController=controllers[0];view.careAnimator.enabled=false;
            care.sprite=stills[0];care.preserveAspect=true;care.raycastTarget=false;
            owner.statePropMotion=view;EditorUtility.SetDirty(owner);EditorUtility.SetDirty(view);EditorUtility.SetDirty(care);EditorUtility.SetDirty(view.careAnimator);
            if(Geometry(originalRects)!=geometry)throw new InvalidOperationException("An existing actor or button RectTransform changed.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(Hash(menuPath)!=menuHash||Hash(scenePath+".meta")!=metaHash)throw new InvalidOperationException("MainMenu or original scene meta changed.");
            if(opened)EditorSceneManager.CloseScene(scene,true);
            report.Add("PASS Existing resident/button geometry, MainMenu and scene meta preserved; no scene regeneration.");
            report.Add("COMPLETE "+DateTime.Now.ToString("O"));report.Add("Backup="+backup);Directory.CreateDirectory(Folder);
            File.WriteAllLines(Folder+"/Install.txt",report);File.WriteAllLines("Library/state-prop-motion-install.txt",report);
        }
        static void Prepare(Animator animator)
        { animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false; }
        static void ImportFrame(string path,int maximum)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new InvalidOperationException("No texture importer: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spritePivot=new Vector2(.5f,.5f);importer.SetTextureSettings(settings);
            importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.maxTextureSize=maximum;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.crunchedCompression=false;
            var platform=importer.GetDefaultPlatformTextureSettings();platform.maxTextureSize=maximum;platform.format=TextureImporterFormat.RGBA32;
            platform.textureCompression=TextureImporterCompression.Uncompressed;platform.crunchedCompression=false;importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();
        }
    }
}
