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
    // Adds only the three completed alternatives; never rebuilds the resident scene.
    public static class ResidentMotionAdditionsInstaller
    {
        const string Root="Assets/EveryoneRecommends/ResidentMotion/Variants";
        const string ReportFolder="Builds/Verification/ResidentMotionAdditions";
        static readonly string[] Ids={"FoodHappy","GameFocused","NewsUnfold"};
        static readonly int[] Topics={1,2,3};

        [Serializable] sealed class ActorGeometry
        {
            public Vector2 anchorMin,anchorMax,anchoredPosition,sizeDelta,pivot;
            public Vector3 position,scale,euler;
            public ActorGeometry(RectTransform rect)
            {
                anchorMin=rect.anchorMin;anchorMax=rect.anchorMax;anchoredPosition=rect.anchoredPosition;sizeDelta=rect.sizeDelta;pivot=rect.pivot;
                position=rect.localPosition;scale=rect.localScale;euler=rect.localEulerAngles;
            }
        }
        static string Geometry(ResidentsView view)=>string.Join("\n",view.residents.Select(image=>JsonUtility.ToJson(new ActorGeometry(image.rectTransform))));
        static string BaseReferences(ResidentsView view)=>string.Join("\n",view.motions.Select(motion=>string.Join(",",motion.controllers.Select(value=>AssetDatabase.GetAssetPath(value)))));
        static string Hash(string path)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
        }
        [MenuItem("Everyone Recommends/Install Resident Motion Additions")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing resident motion additions.");
            const string path="Assets/Scenes/Residents.scene",menu="Assets/Scenes/MainMenu.scene";
            string menuHash=Hash(menu),sceneMetaHash=Hash(path+".meta");
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_ResidentMotionInstaller";
            Directory.CreateDirectory(backup);File.Copy(path,backup+"/Residents.scene",true);
            if(scene.isDirty)EditorSceneManager.SaveScene(scene,backup+"/Residents_Unsaved.scene",true);
            var view=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ResidentsView>(true)).Single();
            if(view.motions==null||view.motions.Length!=9||view.motions.Any(motion=>!motion))throw new InvalidOperationException("Nine existing resident motion drivers are required.");
            string originalGeometry=Geometry(view),originalReferences=BaseReferences(view);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var alternatives=new RuntimeAnimatorController[5];var stills=new Sprite[5];var report=new List<string>();
            for(int action=0;action<Ids.Length;action++)
            {
                string id=Ids[action],folder=Root+"/"+id;var frames=new Sprite[121];
                for(int frame=0;frame<frames.Length;frame++)
                {
                    string image=folder+"/Frames/"+id+"_"+frame.ToString("0000")+".png";
                    if(!File.Exists(image))throw new FileNotFoundException("Missing completed animation frame",image);
                    ImportFrame(image);frames[frame]=AssetDatabase.LoadAssetAtPath<Sprite>(image);
                    if(!frames[frame])throw new InvalidOperationException("Frame did not import as Sprite: "+image);
                }
                string clipPath=folder+"/"+id+"_UI.anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(!clip) { clip=new AnimationClip{name=id+"_UI"};AssetDatabase.CreateAsset(clip,clipPath); }
                clip.ClearCurves();clip.frameRate=24;
                // Sprite-reference clips include the final sample's frame duration.
                // Appending another first-frame key would lengthen the native loop.
                var keys=new ObjectReferenceKeyframe[121];
                for(int frame=0;frame<121;frame++)keys[frame]=new ObjectReferenceKeyframe{time=frame/24f,value=frames[frame]};
                AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(Image),propertyName="m_Sprite"},keys);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.startTime=0;settings.stopTime=121/24f;
                AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
                string controllerPath=folder+"/"+id+"_UI.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;var state=machine.defaultState;
                if(!state)state=machine.AddState("Play");state.motion=clip;state.speed=1;machine.defaultState=state;
                EditorUtility.SetDirty(state);EditorUtility.SetDirty(machine);EditorUtility.SetDirty(controller);
                alternatives[Topics[action]]=controller;stills[Topics[action]]=frames[0];
                report.Add("PASS "+id+": 121 frames at 24 fps, full native-duration loop, root Image sprite curve only, imported max256 RGBA.");
            }
            foreach(var motion in view.motions)
            {
                motion.engagedControllers=(RuntimeAnimatorController[])alternatives.Clone();motion.engagedStills=(Sprite[])stills.Clone();EditorUtility.SetDirty(motion);
            }
            if(Geometry(view)!=originalGeometry||BaseReferences(view)!=originalReferences)throw new InvalidOperationException("Resident geometry or base animation references changed.");
            report.Add("PASS Nine original actor RectTransforms and base controller references preserved.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(Hash(menu)!=menuHash||Hash(path+".meta")!=sceneMetaHash)throw new InvalidOperationException("MainMenu or Residents scene meta unexpectedly changed.");
            report.Add("PASS MainMenu and existing Residents scene meta unchanged.");
            if(opened)EditorSceneManager.CloseScene(scene,true);
            Directory.CreateDirectory(ReportFolder);report.Add("COMPLETE "+DateTime.Now.ToString("O"));report.Add("Backup="+backup);
            File.WriteAllLines(ReportFolder+"/Install.txt",report);File.WriteAllLines("Library/resident-motion-additions-install.txt",report);
            Debug.Log("[ResidentMotion] Three completed visual alternatives installed without replacing base actions.");
        }
        static void ImportFrame(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("No texture importer for "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.spritePivot=new Vector2(.5f,.5f);
            var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType=SpriteMeshType.FullRect;textureSettings.spriteAlignment=(int)SpriteAlignment.Center;
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;
            importer.maxTextureSize=256;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.crunchedCompression=false;
            var platform=importer.GetDefaultPlatformTextureSettings();platform.maxTextureSize=256;platform.format=TextureImporterFormat.RGBA32;
            platform.textureCompression=TextureImporterCompression.Uncompressed;platform.crunchedCompression=false;importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
        }
    }
}
