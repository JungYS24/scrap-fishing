using ScrapFishing.Boat;
using ScrapFishing.Core;
using ScrapFishing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Build
{
    public static class SceneUiBuilder
    {
        const string ScenePath = "Assets/Scenes/Boat.unity";
        const int UiLayer = 5;
        static readonly string[] Generated = { "DepthGauge", "ChipText", "ReadyPanel" };

        [MenuItem("Scrap Fishing/Build Scene UI")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Boat 씬에 Canvas가 없습니다.");
                return;
            }

            if (HasGenerated(canvas.transform) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Build Scene UI", "이미 만들어진 UI가 있습니다. Inspector에서 바꾼 값은 사라집니다. 다시 만들까요?", "다시 만들기", "취소"))
            {
                return;
            }

            foreach (var name in Generated)
            {
                var old = canvas.transform.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            ConfigureScaler(canvas);
            var root = (RectTransform)canvas.transform;
            var gauge = BuildGauge(root);
            var chip = BuildChip(root);
            var ready = BuildReady(root, out var dollar, out var upgrades);

            var ui = canvas.GetComponent<BoatCanvas>();
            if (ui == null)
            {
                ui = canvas.gameObject.AddComponent<BoatCanvas>();
            }

            var so = new SerializedObject(ui);
            so.FindProperty("depthGauge").objectReferenceValue = gauge;
            so.FindProperty("chipText").objectReferenceValue = chip;
            so.FindProperty("readyPanel").objectReferenceValue = ready;
            so.FindProperty("dollarText").objectReferenceValue = dollar;
            var list = so.FindProperty("upgradeButtons");
            list.arraySize = upgrades.Length;
            for (var i = 0; i < upgrades.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = upgrades[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Scene UI built: " + ScenePath);
        }

        [MenuItem("Scrap Fishing/Debug/Add 1000 Dollars")]
        static void AddDollars()
        {
            Progression.AddDollars(1000);
            Debug.Log("Dollars: " + Progression.Dollars);
        }

        [MenuItem("Scrap Fishing/Debug/Reset Progress")]
        static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("Dollar");
            foreach (UpgradeKind kind in System.Enum.GetValues(typeof(UpgradeKind)))
            {
                PlayerPrefs.DeleteKey("Upgrade" + kind);
            }

            PlayerPrefs.Save();
            Debug.Log("Dollar and upgrade levels reset.");
        }

        static bool HasGenerated(Transform canvas)
        {
            foreach (var name in Generated)
            {
                if (canvas.Find(name) != null)
                {
                    return true;
                }
            }

            return false;
        }

        static void ConfigureScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameView.Width, GameView.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = GameView.CanvasMatch;
        }

        static DepthGauge BuildGauge(RectTransform parent)
        {
            var rect = CreateRect(parent, "DepthGauge", new Vector2(0.905f, 0.07f), new Vector2(0.945f, 0.47f));
            CreateImage(rect, "Track", Vector2.zero, Vector2.one, new Color(0.08f, 0.1f, 0.16f, 0.85f));
            var locked = CreateImage(rect, "LockedArea", Vector2.zero, new Vector2(1f, 0.51f), new Color(0.35f, 0.05f, 0.25f, 0.75f));
            var marker = CreateImage(rect, "Marker", new Vector2(0f, 0.98f), new Vector2(1f, 0.98f), Palette.NeonGreen);
            marker.rectTransform.sizeDelta = new Vector2(14f, 12f);

            var gauge = rect.gameObject.AddComponent<DepthGauge>();
            var so = new SerializedObject(gauge);
            so.FindProperty("marker").objectReferenceValue = marker.rectTransform;
            so.FindProperty("markerImage").objectReferenceValue = marker;
            so.FindProperty("lockedArea").objectReferenceValue = locked.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetLayer(rect);
            return gauge;
        }

        static Text BuildChip(RectTransform parent)
        {
            var text = CreateText(parent, "ChipText", 18, TextAnchor.UpperLeft, new Vector2(0.05f, 0.90f), new Vector2(0.5f, 0.98f), Palette.NeonGreen);
            text.text = "0 CY";
            SetLayer(text.transform);
            return text;
        }

        static GameObject BuildReady(RectTransform parent, out Text dollar, out UpgradeButton[] upgrades)
        {
            var panel = CreateRect(parent, "ReadyPanel", Vector2.zero, Vector2.one);
            dollar = CreateText(panel, "DollarText", 22, TextAnchor.UpperRight, new Vector2(0.5f, 0.90f), new Vector2(0.95f, 0.98f), Palette.NeonGreen);
            dollar.text = "$ 0";

            var bar = CreateRect(panel, "UpgradeBar", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.17f));
            var kinds = new[] { UpgradeKind.Reel, UpgradeKind.Boat, UpgradeKind.Depth };
            var names = new[] { "ReelUpgradeButton", "BoatUpgradeButton", "DepthUpgradeButton" };
            upgrades = new UpgradeButton[kinds.Length];
            for (var i = 0; i < kinds.Length; i++)
            {
                var min = new Vector2(i / 3f + 0.015f, 0f);
                var max = new Vector2((i + 1) / 3f - 0.015f, 1f);
                upgrades[i] = BuildUpgradeButton(bar, names[i], kinds[i], min, max);
            }

            SetLayer(panel);
            return panel.gameObject;
        }

        static UpgradeButton BuildUpgradeButton(RectTransform parent, string name, UpgradeKind kind, Vector2 min, Vector2 max)
        {
            var background = CreateImage(parent, name, min, max, Palette.Panel);
            background.raycastTarget = true;
            var go = background.gameObject;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Palette.NeonGreen;
            outline.effectDistance = new Vector2(2f, -2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var rect = background.rectTransform;
            var nameText = CreateText(rect, "NameText", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0.62f), new Vector2(1f, 1f), Palette.Text);
            nameText.text = Progression.DisplayName(kind);
            var levelText = CreateText(rect, "LevelText", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0.36f), new Vector2(1f, 0.62f), Palette.Cyan);
            levelText.text = "Lv 0/" + Progression.MaxLevel(kind);
            var costText = CreateText(rect, "CostText", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0.04f), new Vector2(1f, 0.36f), Palette.NeonGreen);
            costText.text = "$ 0";

            var upgrade = go.AddComponent<UpgradeButton>();
            var so = new SerializedObject(upgrade);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return upgrade;
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect, min, max);
            return rect;
        }

        static Image CreateImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = CreateRect(parent, name, min, max).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color color)
        {
            var text = UiFonts.CreateText(parent, name, size, anchor);
            Stretch(text.rectTransform, min, max);
            text.color = color;
            return text;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetLayer(Transform root)
        {
            root.gameObject.layer = UiLayer;
            foreach (Transform child in root)
            {
                SetLayer(child);
            }
        }
    }
}
