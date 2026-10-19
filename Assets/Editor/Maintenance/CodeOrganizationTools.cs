using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.EditorTools
{
    // One-time folder migration; Unity moves each .meta with its asset.
    // Never changes a class/namespace or saves a scene.
    public static class CodeOrganizationTools
    {
        const string Folder = "Builds/Verification/CodeOrganization";
        const string ManifestPath = Folder + "/Migration.json";
        [Serializable] sealed class Entry
        {
            public string from, to, guid, sourceHash, metaHash;
            public Entry(string from, string to) { this.from=from; this.to=to; }
        }
        [Serializable] sealed class Manifest
        {
            public string created;
            public Entry[] scripts;
        }
        static Entry[] Plan() => new[]
        {
            new Entry("Assets/Scripts/UI/FrontendServices.cs", "Assets/Scripts/Core/FrontendServices.cs"),
            new Entry("Assets/Scripts/GameFlow/ResidentRun.cs", "Assets/Scripts/GameFlow/Simulation/ResidentRun.cs"),
            new Entry("Assets/Scripts/GameFlow/EndingCatalog.cs", "Assets/Scripts/GameFlow/Endings/EndingCatalog.cs"),
            new Entry("Assets/Scripts/GameFlow/EndingAchievements.cs", "Assets/Scripts/GameFlow/Endings/EndingAchievements.cs"),
            new Entry("Assets/Scripts/UI/ApartmentBackdrop.cs", "Assets/Scripts/Presentation/Building/ApartmentBackdrop.cs"),
            new Entry("Assets/Scripts/UI/FloorDetailView.cs", "Assets/Scripts/Presentation/Building/FloorDetailView.cs"),
            new Entry("Assets/Scripts/UI/FloorSceneDefinition.cs", "Assets/Scripts/Presentation/Building/FloorSceneDefinition.cs"),
            new Entry("Assets/Scripts/UI/FloorInteractionAnchor.cs", "Assets/Scripts/Presentation/Building/FloorInteractionAnchor.cs"),
            new Entry("Assets/Scripts/UI/ResidentMotion.cs", "Assets/Scripts/Presentation/Residents/ResidentMotion.cs"),
            new Entry("Assets/Scripts/UI/MenuView.cs", "Assets/Scripts/UI/MainMenu/MenuView.cs"),
            new Entry("Assets/Scripts/UI/MenuEntrance.cs", "Assets/Scripts/UI/MainMenu/MenuEntrance.cs"),
            new Entry("Assets/Scripts/UI/SettingsView.cs", "Assets/Scripts/UI/Settings/SettingsView.cs"),
            new Entry("Assets/Scripts/UI/AchievementsView.cs", "Assets/Scripts/UI/Achievements/AchievementsView.cs"),
            new Entry("Assets/Scripts/UI/AchievementCardView.cs", "Assets/Scripts/UI/Achievements/AchievementCardView.cs"),
            new Entry("Assets/Scripts/UI/ResidentsView.cs", "Assets/Scripts/UI/Residents/ResidentsView.cs"),
            new Entry("Assets/Scripts/UI/ResidentStatusCard.cs", "Assets/Scripts/UI/Residents/ResidentStatusCard.cs"),
            new Entry("Assets/Scripts/UI/StageProgressView.cs", "Assets/Scripts/UI/Residents/StageProgressView.cs"),
            new Entry("Assets/Scripts/UI/DemoOverlay.cs", "Assets/Scripts/UI/Residents/DemoOverlay.cs"),
            new Entry("Assets/Scripts/UI/UIFeedbackProfile.cs", "Assets/Scripts/UI/Feedback/UIFeedbackProfile.cs"),
            new Entry("Assets/Scripts/UI/UiButtonFeedback.cs", "Assets/Scripts/UI/Feedback/UiButtonFeedback.cs"),
            new Entry("Assets/Scripts/UI/UiClickSound.cs", "Assets/Scripts/UI/Feedback/UiClickSound.cs"),
            new Entry("Assets/Scripts/UI/ResidentReactionBubble.cs", "Assets/Scripts/UI/Feedback/ResidentReactionBubble.cs"),
            new Entry("Assets/Scripts/UI/RecommendationDeliveryView.cs", "Assets/Scripts/UI/Feedback/RecommendationDeliveryView.cs"),
            new Entry("Assets/Scripts/UI/CharacterSceneWipe.cs", "Assets/Scripts/UI/Transitions/CharacterSceneWipe.cs"),
            new Entry("Assets/Scripts/UI/PopupSlide.cs", "Assets/Scripts/UI/Transitions/PopupSlide.cs"),
            new Entry("Assets/Scripts/UI/SoftCircleGraphic.cs", "Assets/Scripts/UI/Graphics/SoftCircleGraphic.cs"),
            new Entry("Assets/Scripts/UI/DeliveryArrowGraphic.cs", "Assets/Scripts/UI/Graphics/DeliveryArrowGraphic.cs"),
            new Entry("Assets/Scripts/UI/AttitudeFaceGraphic.cs", "Assets/Scripts/UI/Graphics/AttitudeFaceGraphic.cs"),
            new Entry("Assets/Scripts/UI/FrontendSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/FrontendSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/ResidentsGameplaySmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/ResidentsGameplaySmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/DemoSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/DemoSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/VisualPolishSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/VisualPolishSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/PlayerExperienceSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/PlayerExperienceSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/BackgroundLightingSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/BackgroundLightingSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/SingleBackdropSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/SingleBackdropSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/FloorDetailSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/FloorDetailSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/EndingsSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/EndingsSmokeCheck.cs"),
            new Entry("Assets/Scripts/UI/UIPolishSmokeCheck.cs", "Assets/Scripts/Diagnostics/PlayMode/UIPolishSmokeCheck.cs"),
            new Entry("Assets/Editor/FrontendSceneBuilder.cs", "Assets/Editor/BuildTools/FrontendSceneBuilder.cs"),
            new Entry("Assets/Editor/FrontendPolish.cs", "Assets/Editor/SceneSetup/FrontendPolish.cs"),
            new Entry("Assets/Editor/FrontendExitButtonInstaller.cs", "Assets/Editor/SceneSetup/FrontendExitButtonInstaller.cs"),
            new Entry("Assets/Editor/ResidentsGameplayInstaller.cs", "Assets/Editor/SceneSetup/ResidentsGameplayInstaller.cs"),
            new Entry("Assets/Editor/DemoSceneInstaller.cs", "Assets/Editor/SceneSetup/DemoSceneInstaller.cs"),
            new Entry("Assets/Editor/ResidentsVisualPolish.cs", "Assets/Editor/SceneSetup/ResidentsVisualPolish.cs"),
            new Entry("Assets/Editor/PlayerExperienceInstaller.cs", "Assets/Editor/SceneSetup/PlayerExperienceInstaller.cs"),
            new Entry("Assets/Editor/SingleBackdropInstaller.cs", "Assets/Editor/SceneSetup/SingleBackdropInstaller.cs"),
            new Entry("Assets/Editor/FloorDetailInstaller.cs", "Assets/Editor/SceneSetup/FloorDetailInstaller.cs"),
            new Entry("Assets/Editor/EndingsInstaller.cs", "Assets/Editor/SceneSetup/EndingsInstaller.cs"),
            new Entry("Assets/Editor/UIPolishInstaller.cs", "Assets/Editor/SceneSetup/UIPolishInstaller.cs")
        };

        [MenuItem("Everyone Recommends/Code/Organize Script Folders")]
        public static void Organize()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before moving scripts.");
            Directory.CreateDirectory(Folder);
            var plan=Plan();
            var pending=plan.Where(e=>File.Exists(e.from)).ToArray();
            if(pending.Length==0) { Validate(); return; }
            foreach(var entry in pending)
            {
                if(File.Exists(entry.to))throw new IOException("Destination already exists: "+entry.to);
                entry.guid=AssetDatabase.AssetPathToGUID(entry.from);
                entry.sourceHash=Hash(entry.from); entry.metaHash=Hash(entry.from+".meta");
                if(string.IsNullOrEmpty(entry.guid))throw new IOException("Asset not imported: "+entry.from);
            }
            // The manifest is also a recovery map if the editor is interrupted.
            var manifest=new Manifest { created=DateTime.Now.ToString("O"),scripts=pending };
            File.WriteAllText(ManifestPath,JsonUtility.ToJson(manifest,true));
            foreach(var folder in pending.Select(e=>Path.GetDirectoryName(e.to).Replace('\\','/')).Distinct())
                EnsureFolder(folder);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(var entry in pending)
                {
                    var error=AssetDatabase.MoveAsset(entry.from,entry.to);
                    if(!string.IsNullOrEmpty(error))throw new IOException(error);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            File.WriteAllText(Folder+"/Moved.txt",DateTime.Now.ToString("O")+"\nMoved="+pending.Length);
            Debug.Log("[Code] Moved "+pending.Length+" scripts with their .meta files. Validate after compilation.");
        }

        [MenuItem("Everyone Recommends/Code/Validate Script References")]
        public static void Validate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before reference validation.");
            Directory.CreateDirectory(Folder);
            var report=new List<string>();
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            foreach(var entry in manifest.scripts)
            {
                if(!File.Exists(entry.to)||File.Exists(entry.from)||Hash(entry.to)!=entry.sourceHash||
                   Hash(entry.to+".meta")!=entry.metaHash||AssetDatabase.AssetPathToGUID(entry.to)!=entry.guid)
                    throw new IOException("Script or .meta changed during migration: "+entry.to);
            }
            report.Add("PASS "+manifest.scripts.Length+" scripts: contents, .meta bytes and GUID preserved.");
            var scenePaths=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            var originalActive=SceneManager.GetActiveScene();
            foreach(var path in scenePaths)
            {
                var scene=SceneManager.GetSceneByPath(path);
                bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    foreach(var root in scene.GetRootGameObjects())CheckObject(root);
                    report.Add("PASS No missing scripts: "+path);
                }
                finally
                {
                    if(opened)EditorSceneManager.CloseScene(scene,true);
                    if(originalActive.IsValid()&&originalActive.isLoaded)SceneManager.SetActiveScene(originalActive);
                }
            }
            int prefabCount=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/EveryoneRecommends"}))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if(prefab) { CheckObject(prefab); prefabCount++; }
            }
            report.Add("PASS No missing scripts in "+prefabCount+" project prefabs.");
            foreach(var path in new[]{"Assets/Resources/UIFeedback/Default.asset","Assets/Resources/Endings/EndingCatalog.asset"})
            {
                var asset=AssetDatabase.LoadMainAssetAtPath(path);
                if(!asset||asset.GetType()==typeof(UnityEngine.Object))throw new IOException("Unresolved configuration: "+path);
                report.Add("PASS Configuration type: "+asset.GetType().Name);
            }
            report.Add("COMPLETE "+DateTime.Now.ToString("O"));
            File.WriteAllLines(Folder+"/References.txt",report);
            Debug.Log("[Code] Script classification and serialized references verified.");
        }
        static void CheckObject(GameObject root)
        {
            foreach(var item in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)>0)
                    throw new IOException("Missing script: "+root.name+"/"+item.name);
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static string Hash(string path)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}

