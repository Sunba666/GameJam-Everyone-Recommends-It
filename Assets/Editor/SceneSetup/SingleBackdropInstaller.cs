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
    public static class SingleBackdropInstaller
    {
        const string ImagePath="Assets/Art/Building/Building_CompleteDay_v2.png";
        const string MaterialPath="Assets/Art/Building/ApartmentLighting.mat";
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before replacing the backdrop.");
            PrepareAssets();
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Residents.scene");
            if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Residents.scene",OpenSceneMode.Single);
            string backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_SingleBackdrop";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            Attach(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Library/single-backdrop-install.txt",DateTime.Now.ToString("O")+"\nOne continuous clean background sprite, no header/footer strips; lamp lighting material installed.\nBackup="+backup);
        }
        static void PrepareAssets()
        {
            AssetDatabase.ImportAsset(ImagePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(ImagePath);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            AssetDatabase.ImportAsset("Assets/Shaders/UI/ApartmentLighting.shader",ImportAssetOptions.ForceSynchronousImport);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/UI/ApartmentLighting.shader");
            if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Apartment lighting shader did not compile.");
            if(!AssetDatabase.LoadAssetAtPath<Material>(MaterialPath))AssetDatabase.CreateAsset(new Material(shader),MaterialPath);
        }
        public static void Attach(ResidentsView view)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(!sprite||!material) { PrepareAssets();sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath); }
            var art=view.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Artboard_1672x941");
            foreach(string name in new[]{"FixedBuildingHeader","FixedBuildingFooter"}) { var old=art.Find(name);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject); }
            view.buildingViewport.anchoredPosition=new Vector2(-220,0);view.buildingViewport.sizeDelta=new Vector2(1228,941);
            view.buildingContent.anchoredPosition=new Vector2(220,0);view.buildingContent.localScale=Vector3.one;
            var image=view.buildingContent.GetComponentsInChildren<Image>(true).Single(i=>i.name=="BuildingArtwork");
            image.sprite=sprite;image.color=Color.white;image.material=material;image.raycastTarget=false;
            image.rectTransform.anchoredPosition=Vector2.zero;image.rectTransform.sizeDelta=new Vector2(1672,941);image.rectTransform.localScale=Vector3.one;image.transform.SetAsFirstSibling();
            var backdrop=image.GetComponent<ApartmentBackdrop>();if(!backdrop)backdrop=image.gameObject.AddComponent<ApartmentBackdrop>();
            backdrop.owner=view;backdrop.background=image;backdrop.lightingMaterial=material;view.apartmentBackdrop=backdrop;
            if(view.buildingTint) { view.buildingTint.color=Color.clear;view.buildingTint.gameObject.SetActive(false);view.buildingTint.name="LegacyUniformTint_Disabled"; }
            var logo=art.Find("FixedGameLogo");
            if(!logo)
            {
                var go=new GameObject("FixedGameLogo",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=5;
                var rect=go.GetComponent<RectTransform>();rect.SetParent(art,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
                rect.anchoredPosition=new Vector2(-690,421.5f);rect.sizeDelta=new Vector2(280,98);
                var graphic=go.GetComponent<Image>();graphic.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EveryoneRecommends/MainMenu/Art/Title_LogoAndTagline.png");graphic.preserveAspect=true;graphic.raycastTarget=false;
            }
            EditorUtility.SetDirty(backdrop);EditorUtility.SetDirty(image);EditorUtility.SetDirty(view);
        }
    }
}
