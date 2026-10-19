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
    public static class PlayerExperienceInstaller
    {
        static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing the player-experience update.");
            ImportAudio();
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_Player";Directory.CreateDirectory(backup);
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/FrontendServices.prefab");
            AudioReferences(prefab.GetComponent<FrontendServices>());
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/UI/FrontendServices.prefab");PrefabUtility.UnloadPrefabContents(prefab);
            foreach(string path in new[]{"Assets/Scenes/MainMenu.scene","Assets/Scenes/Residents.scene"})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                EditorSceneManager.SaveScene(scene,backup+"/"+scene.name+".scene",true);
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var service in root.GetComponentsInChildren<FrontendServices>(true))
                    { AudioReferences(service);EditorUtility.SetDirty(service);PrefabUtility.RecordPrefabInstancePropertyModifications(service); }
                    foreach(var view in root.GetComponentsInChildren<ResidentsView>(true))Attach(view);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                if(opened)EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.SaveAssets();File.WriteAllText("Library/player-experience-install.txt",DateTime.Now.ToString("O")+"\nNine residents, music, earcons, share arrows and observation controls installed.\nBackup="+backup);
        }
        static void ImportAudio()
        {
            foreach(string path in new[]{"Assets/Audio/Music/ResidentsWindowLoop.wav"}.Concat(new[]{"Cat","Food","Game","News","Music"}.Select(n=>"Assets/Audio/SFX/Recommend_"+n+".wav")))
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);bool music=path.Contains("/Music/");
                var settings=importer.defaultSampleSettings;settings.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=music?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;settings.quality=.65f;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=!music;
                importer.defaultSampleSettings=settings;importer.forceToMono=!music;importer.SaveAndReimport();
            }
        }
        static void AudioReferences(FrontendServices service)
        {
            service.residentsMusic=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/ResidentsWindowLoop.wav");
            service.recommendationSounds=new[]{"Cat","Food","Game","News","Music"}.Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Recommend_"+n+".wav")).ToArray();
        }
        public static void Attach(ResidentsView view)
        {
            if(view.residents.Length==10)
            {
                if(view.residents[9].name!="Resident_10")throw new InvalidOperationException("Unexpected tenth actor. Do not remove a different resident.");
                var actor=view.residents[9].gameObject;var node=view.statusIcons[9].gameObject;
                view.residents=view.residents.Take(9).ToArray();view.statusIcons=view.statusIcons.Take(9).ToArray();
                view.motions=view.motions.Take(9).ToArray();view.reactionLabels=view.reactionLabels.Take(9).ToArray();view.recommendedLabels=view.recommendedLabels.Take(9).ToArray();
                view.propImages=view.propImages.Take(45).ToArray();
                view.delivery.receiptIcons=view.delivery.receiptIcons.Take(9).ToArray();view.delivery.receiptLabels=view.delivery.receiptLabels.Take(9).ToArray();
                UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(node);
            }
            if(view.residents.Length!=9)throw new InvalidOperationException("This Demo expects three floors with three residents each.");
            var art=view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Artboard_1672x941");
            view.roomLabels=new Text[9];
            for(int i=0;i<9;i++)
            {
                view.statusIcons[i].rectTransform.anchoredPosition=new Vector2(495+i%3*125,205-i/3*135);
                var existing=view.statusIcons[i].transform.Find("RoomNumber");
                var label=existing?existing.GetComponent<Text>():Label("RoomNumber",view.statusIcons[i].transform,new Vector2(0,-44),new Vector2(92,23),16);
                label.text=ResidentRun.RoomOf(i);view.roomLabels[i]=label;
                view.recommendedLabels[i].rectTransform.anchoredPosition=new Vector2(0,-66);view.recommendedLabels[i].fontSize=14;
            }
            if(!view.paceButton)
            {
                var go=UnityEngine.Object.Instantiate(view.pauseButton.gameObject,art);go.name="ObservationPace";
                view.paceButton=go.GetComponent<Button>();view.paceButton.onClick=new Button.ButtonClickedEvent();view.paceLabel=go.GetComponentInChildren<Text>();
            }
            var pace=(RectTransform)view.paceButton.transform;pace.anchoredPosition=new Vector2(-365,350);pace.sizeDelta=new Vector2(164,40);
            view.paceLabel.text="观察 ×1";view.paceLabel.fontSize=20;view.paceLabel.rectTransform.sizeDelta=new Vector2(142,32);
            view.paceLabel.resizeTextForBestFit=true;view.paceLabel.resizeTextMaxSize=20;view.paceLabel.resizeTextMinSize=18;
            view.statusCard.reservedAreas=view.statusCard.reservedAreas.Where(r=>r).Concat(new[]{pace}).Distinct().ToArray();
            view.statusCard.details.resizeTextMinSize=13;
            if(view.simulation.thresholdVariation<=.001f)view.simulation.thresholdVariation=.18f;
            var delivery=view.delivery;
            if(!delivery.worldArrow)
            {
                var rect=Rect("NeighbourWorldArrow",delivery.transform,Vector2.zero,new Vector2(1672,941));
                delivery.worldArrow=rect.gameObject.AddComponent<DeliveryArrowGraphic>();delivery.worldArrow.raycastTarget=false;
            }
            if(!delivery.routeLabel)delivery.routeLabel=Label("DeliveryRoute",delivery.transform,new Vector2(620,260),new Vector2(378,28),18);
            delivery.worldArrow.gameObject.SetActive(false);delivery.routeLabel.gameObject.SetActive(false);
            EditorUtility.SetDirty(delivery);EditorUtility.SetDirty(view.statusCard);EditorUtility.SetDirty(view);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));go.layer=5;
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        static Text Label(string name,Transform parent,Vector2 position,Vector2 size,int fontSize)
        {
            var label=Rect(name,parent,position,size).gameObject.AddComponent<Text>();label.font=Font;label.fontSize=fontSize;
            label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.16f,.13f,.10f);label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
    }
}
