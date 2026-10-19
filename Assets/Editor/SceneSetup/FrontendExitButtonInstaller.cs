using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EveryoneRecommends.Frontend;

namespace EveryoneRecommends.EditorTools
{
    public static class FrontendExitButtonInstaller
    {
        const string ScenePath = "Assets/Scenes/MainMenu.scene";
        const string Art = "Assets/EveryoneRecommends/SettingsUI/Art/03_按钮底板/Button_Secondary_";

        [MenuItem("Everyone Recommends/Add Exit Button To Existing Menu")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before editing the menu.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string backup = "Design/MenuExitBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "/MainMenu.scene");
            if (scene.isDirty) EditorSceneManager.SaveScene(scene, backup + "/MainMenu_Unsaved.scene", true);
            MenuView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<MenuView>(true);
                if (view) break;
            }
            if (!view) throw new InvalidOperationException("MainMenu is missing MenuView.");
            Attach(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/frontend-exit-button.txt", DateTime.Now.ToString("O") + "\nExit button saved and wired; existing scene preserved.\nBackup: " + backup);
            Debug.Log("[Frontend] Exit button added to existing menu.");
        }

        public static void Attach(MenuView view)
        {
            if (view.quit)
            {
                var existingLabel = view.quit.GetComponentInChildren<Text>();
                existingLabel.fontStyle = FontStyle.Bold; existingLabel.fontSize = 32;
                EditorUtility.SetDirty(existingLabel);
                return;
            }
            var reference = (RectTransform)view.settings.transform;
            var obj = new GameObject("Button_Quit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(UiClickSound));
            obj.layer = reference.gameObject.layer;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(reference.parent, false);
            rect.anchorMin = reference.anchorMin; rect.anchorMax = reference.anchorMax; rect.pivot = reference.pivot;
            rect.sizeDelta = reference.sizeDelta;
            var image = obj.GetComponent<Image>();
            image.sprite = LoadSprite("Normal"); image.type = Image.Type.Simple; image.raycastTarget = true;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = LoadSprite("Hover"), pressedSprite = LoadSprite("Pressed"), disabledSprite = LoadSprite("Disabled") };
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.layer = obj.layer;
            var textRect = label.GetComponent<RectTransform>(); textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 4); textRect.offsetMax = new Vector2(-8, -4);
            var text = label.GetComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
            text.text = "退出游戏"; text.fontSize = 32; text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.1f, .085f, .075f); text.raycastTarget = false;
            view.quit = button;

            // Extend the existing centered row, changing only its horizontal positions.
            float center = reference.anchoredPosition.x;
            float spacing = Mathf.Abs(((RectTransform)view.achievements.transform).anchoredPosition.x - center);
            if (spacing < reference.sizeDelta.x) spacing = reference.sizeDelta.x + 10;
            var row = new[] { view.howTo, view.settings, view.achievements, view.quit };
            for (int i = 0; i < row.Length; i++)
            {
                var item = (RectTransform)row[i].transform;
                item.anchoredPosition = new Vector2(center + (i - 1.5f) * spacing, reference.anchoredPosition.y);
                EditorUtility.SetDirty(item);
            }
            EditorUtility.SetDirty(view);
        }

        static Sprite LoadSprite(string state)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + state + ".png");
            if (!sprite) throw new InvalidOperationException("Missing button art: " + state);
            return sprite;
        }
    }
}
