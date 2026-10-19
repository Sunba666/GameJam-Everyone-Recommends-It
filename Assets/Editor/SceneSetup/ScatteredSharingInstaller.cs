using System;
using System.IO;
using System.Linq;
using EveryoneRecommends.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveryoneRecommends.EditorTools
{
    public static class ScatteredSharingInstaller
    {
        [MenuItem("Everyone Recommends/Install Scattered Sharing Feedback")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing share feedback.");
            const string path="Assets/Scenes/Residents.scene";
            var original=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var backup="Design/DemoBackup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_SharingScene";Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(scene,backup+"/Residents.scene",true);
            var owner=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ResidentsView>(true)).Single();
            Attach(owner);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(opened)EditorSceneManager.CloseScene(scene,true);
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            File.WriteAllText("Library/scattered-sharing-install.txt",DateTime.Now.ToString("O")+"\nNine topic speech balloons; existing resident positions preserved.\nBackup="+backup);
        }
        public static void Attach(ResidentsView owner)
        {
            var delivery=owner.delivery;delivery.receiptBubbles=new RectTransform[owner.residents.Length];
            for(int i=0;i<owner.residents.Length;i++)
            {
                var actor=owner.residents[i].transform;var existing=actor.Find("ReceiptSpeechBubble");
                var root=existing?(RectTransform)existing:new GameObject("ReceiptSpeechBubble",typeof(RectTransform),typeof(CanvasRenderer)).GetComponent<RectTransform>();
                root.SetParent(actor,false);root.gameObject.layer=actor.gameObject.layer;
                root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=new Vector2(80,70);root.sizeDelta=new Vector2(70,66);root.localScale=Vector3.one;
                var graphic=root.GetComponent<ReceiptBubbleGraphic>();if(!graphic)graphic=root.gameObject.AddComponent<ReceiptBubbleGraphic>();graphic.raycastTarget=false;
                var icon=delivery.receiptIcons[i];icon.transform.SetParent(root,false);
                icon.rectTransform.anchoredPosition=new Vector2(0,5);icon.rectTransform.sizeDelta=new Vector2(43,43);icon.rectTransform.localScale=Vector3.one;icon.raycastTarget=false;
                var label=delivery.receiptLabels[i];label.transform.SetParent(root,false);label.rectTransform.anchoredPosition=new Vector2(0,-41);label.rectTransform.sizeDelta=new Vector2(88,20);label.raycastTarget=false;
                delivery.receiptBubbles[i]=root;root.SetAsLastSibling();root.gameObject.SetActive(false);
            }
            if(owner.simulation.shareWindowEnd<=0) { owner.simulation.shareWindowStart=.18f;owner.simulation.shareWindowEnd=.90f;owner.simulation.shareMinimumGap=2.5f; }
            EditorUtility.SetDirty(delivery);EditorUtility.SetDirty(owner);
        }
    }
}
