using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using EveryoneRecommends.Frontend;

namespace EveryoneRecommends.EditorTools
{
    public static class FrontendPolish
    {
        public const string MusicPath = "Assets/Audio/Music/MenuCourtyardLoop.wav";
        [MenuItem("Everyone Recommends/Polish Existing Frontend")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before polishing scenes.");
            string backup="Design/MenuPolishBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            foreach(var p in new[]{"Assets/Scenes/MainMenu.scene","Assets/Scenes/Residents.scene","Assets/Prefabs/UI/FrontendServices.prefab"})
                File.Copy(p,backup+"/"+Path.GetFileName(p));
            int textures=0;
            foreach(var dir in new[]{"Assets/EveryoneRecommends","Assets/Art/ImportedGame"})
            foreach(var p in Directory.GetFiles(dir,"*.png",SearchOption.AllDirectories))
            {
                var path=p.Replace('\\','/');var t=AssetImporter.GetAtPath(path) as TextureImporter;
                if(!t || t.textureType==TextureImporterType.Cursor)continue;
                t.filterMode=FilterMode.Bilinear;t.mipmapEnabled=false;t.alphaIsTransparency=true;
                t.textureCompression=TextureImporterCompression.Uncompressed;t.crunchedCompression=false;t.maxTextureSize=4096;
                t.SaveAndReimport();textures++;
            }
            AssetDatabase.ImportAsset(MusicPath,ImportAssetOptions.ForceSynchronousImport);
            var ai=(AudioImporter)AssetImporter.GetAtPath(MusicPath);
            var sample=ai.defaultSampleSettings;sample.loadType=AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat=AudioCompressionFormat.PCM;sample.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            sample.preloadAudioData=true;ai.defaultSampleSettings=sample;ai.forceToMono=false;ai.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/FrontendServices.prefab");
            prefab.GetComponent<FrontendServices>().musicClip=clip;
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/UI/FrontendServices.prefab");PrefabUtility.UnloadPrefabContents(prefab);
            var active=SceneManager.GetActiveScene();
            foreach(var path in new[]{"Assets/Scenes/MainMenu.scene","Assets/Scenes/Residents.scene"})
            {
                var s=SceneManager.GetSceneByPath(path);bool opened=!s.IsValid()||!s.isLoaded;
                if(opened)s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                if(s.isDirty)EditorSceneManager.SaveScene(s,backup+"/"+s.name+"_Unsaved.scene",true);
                foreach(var root in s.GetRootGameObjects())
                {
                    foreach(var c in root.GetComponentsInChildren<Canvas>(true)){c.pixelPerfect=false;EditorUtility.SetDirty(c);}
                    foreach(var service in root.GetComponentsInChildren<FrontendServices>(true))
                    {service.musicClip=clip;EditorUtility.SetDirty(service);PrefabUtility.RecordPrefabInstancePropertyModifications(service);}
                    foreach(var menu in root.GetComponentsInChildren<MenuView>(true))AttachPopup(menu.achievementsPanel);
                    foreach(var view in root.GetComponentsInChildren<SettingsView>(true))
                    {
                        AttachPopup(view.gameObject);
                        foreach(var slider in new[]{view.master,view.music,view.sfx})
                        {
                            slider.handleRect.sizeDelta=new Vector2(54,0);EditorUtility.SetDirty(slider.handleRect);
                            var img=slider.handleRect.GetComponent<Image>();img.preserveAspect=true;EditorUtility.SetDirty(img);
                            var track=slider.transform.Find("Track").GetComponent<Image>();track.raycastTarget=true;EditorUtility.SetDirty(track);
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
                if(opened)EditorSceneManager.CloseScene(s,true);
            }
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/frontend-polish.txt",DateTime.Now.ToString("O")+"\nBilinear textures="+textures+"\nMusic="+clip.name+"\nBackup="+backup+"\nExisting scene layout preserved.");
            Debug.Log("[Frontend] Polish applied to existing scenes.");
        }
        public static void AttachPopup(GameObject root)
        {
            var slide=root.GetComponent<PopupSlide>()??root.AddComponent<PopupSlide>();
            slide.panel=root.transform.Find("Panel") as RectTransform;
            slide.scrim=root.transform.Find("Scrim").GetComponent<Image>();
            if(!slide.panel)throw new InvalidOperationException("Popup Panel missing: "+root.name);
            EditorUtility.SetDirty(slide);
        }
    }
}
