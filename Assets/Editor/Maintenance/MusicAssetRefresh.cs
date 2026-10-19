using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using EveryoneRecommends.Frontend;
using UnityEditor;
using UnityEngine;

namespace EveryoneRecommends.EditorTools
{
    // Refresh just the two music imports; do not rebuild scenes or rewrite sound effects.
    public static class MusicAssetRefresh
    {
        [MenuItem("Everyone Recommends/Refresh Updated Music")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before refreshing music.");
            string[] paths={"Assets/Audio/Music/MenuCourtyardLoop.wav","Assets/Audio/Music/ResidentsWindowLoop.wav"};
            var clips=new AudioClip[2];var report=new List<string>();
            for(int i=0;i<paths.Length;i++)
            {
                AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                clips[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
                if(!clips[i]||clips[i].length<=0)throw new InvalidOperationException("Music import failed: "+paths[i]);
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(paths[i]))).Replace("-","").ToLowerInvariant();
                report.Add("Imported="+paths[i]+" seconds="+clips[i].length.ToString("R")+" channels="+clips[i].channels+" sha256="+hash);
            }
            const string prefabPath="Assets/Prefabs/UI/FrontendServices.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var service=root.GetComponent<FrontendServices>();
                if(!service)throw new InvalidOperationException("Frontend music service is missing.");
                if(service.musicClip!=clips[0]||service.residentsMusic!=clips[1])
                {
                    string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_MusicReferences";
                    Directory.CreateDirectory(backup);File.Copy(prefabPath,backup+"/FrontendServices.prefab");
                    service.musicClip=clips[0];service.residentsMusic=clips[1];
                    PrefabUtility.SaveAsPrefabAsset(root,prefabPath);report.Add("Updated music references; backup="+backup);
                }
                else report.Add("Existing prefab references already select both updated music assets.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Directory.CreateDirectory("Builds/Verification/AudioGroove");report.Add("COMPLETE "+DateTime.Now.ToString("O"));
            File.WriteAllLines("Builds/Verification/AudioGroove/ReapplyMusic.txt",report);
            File.WriteAllLines("Library/music-refresh.txt",report);
            Debug.Log("[Music] Updated menu and gameplay music reimported; SFX and scenes preserved.");
        }
    }
}
