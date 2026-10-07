using ScrapFishing.Boat;
using ScrapFishing.Core;
using ScrapFishing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Build
{
    public static class BoatSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Boat.unity";
        const string NomadPath = "Assets/Art/Nomad/Nomad.png";
        const string LurePath = "Assets/Art/Rure/Rure_Cyber.png";
        const string TitleLogoPath = "Assets/Art/Title/TitleLogo.png";
        const int UiLayer = 5;

        [MenuItem("Scrap Fishing/Build Boat Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindFirstObjectByType<BoatCanvas>();
            if (canvas == null)
            {
                Debug.LogError("Canvas에 BoatCanvas가 없습니다. 먼저 Scrap Fishing > Build Scene UI를 실행하세요.");
                return;
            }

            if (!Application.isBatchMode && canvas.transform.Find("Title") != null &&
                !EditorUtility.DisplayDialog("Build Boat Scene", "이미 만들어진 타이틀/보트/루어가 있습니다. 씬에서 바꾼 값은 사라집니다. 다시 만들까요?", "다시 만들기", "취소"))
            {
                return;
            }

            DestroyIfExists(canvas.transform.Find("Title"));
            DestroyIfExists(FindRoot("SurfaceProps"));
            DestroyIfExists(FindRoot("Lure"));

            ConfigureSprite(TitleLogoPath, FilterMode.Point);
            var title = BuildTitle((RectTransform)canvas.transform);
            var rodTip = BuildSurface();
            BuildLure(rodTip);

            var so = new SerializedObject(canvas);
            so.FindProperty("title").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.BringReadyToFront();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneVisibilityManager.instance.Hide(title.gameObject, true);
            Debug.Log("Boat scene built: " + ScenePath);
        }

        static TitleView BuildTitle(RectTransform canvas)
        {
            var root = CreateRect(canvas, "Title", Vector2.zero, Vector2.one);
            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);

            var logo = CreateRect(root, "TitleImage", new Vector2(0.06f, 0.6f), new Vector2(0.94f, 0.78f)).gameObject.AddComponent<Image>();
            logo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitleLogoPath);
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            var start = UiButton.Create(root, "StartButton", "출항", new Vector2(0.25f, 0.44f), new Vector2(0.75f, 0.54f), null);

            var howTo = UiFonts.CreateText(root, "HowTo", 16, TextAnchor.MiddleCenter);
            Stretch(howTo.rectTransform, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.42f));
            howTo.text = "깊게 탭할수록 고가\n스와이프 포획\n아래로 스와이프 잠수\n스틱 유영";
            howTo.color = Palette.Cyan;

            var view = root.gameObject.AddComponent<TitleView>();
            var so = new SerializedObject(view);
            so.FindProperty("startButton").objectReferenceValue = start;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetLayer(root, UiLayer);
            return view;
        }

        static Transform BuildSurface()
        {
            var root = new GameObject("SurfaceProps");
            var nomad = CreateSprite(root.transform, "Nomad", AssetDatabase.LoadAssetAtPath<Sprite>(NomadPath), new Vector3(-1.2f, 0.2f, 0f), 0.8f, 5);
            var rodTip = new GameObject("RodTip").transform;
            rodTip.SetParent(nomad, false);
            rodTip.localPosition = new Vector3(0.84f, 0.83f, 0f);
            return rodTip;
        }

        static void BuildLure(Transform rodTip)
        {
            var lure = CreateSprite(null, "Lure", AssetDatabase.LoadAssetAtPath<Sprite>(LurePath), new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f), 0.45f, 6);
            var lineEnd = new GameObject("LineEnd").transform;
            lineEnd.SetParent(lure, false);
            lineEnd.localPosition = new Vector3(0.15f, 0.49f, 0f);

            var hook = lure.gameObject.AddComponent<HookMover>();
            var so = new SerializedObject(hook);
            so.FindProperty("rodTip").objectReferenceValue = rodTip;
            so.FindProperty("lineEnd").objectReferenceValue = lineEnd;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform CreateSprite(Transform parent, string name, Sprite sprite, Vector3 position, float scale, int order)
        {
            if (sprite == null)
            {
                Debug.LogWarning("Sprite not found for " + name);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go.transform;
        }

        static void ConfigureSprite(string path, FilterMode filter)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Transform FindRoot(string name)
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go.transform;
                }
            }

            return null;
        }

        static void DestroyIfExists(Transform target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target.gameObject);
            }
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect, min, max);
            return rect;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayer(child, layer);
            }
        }
    }
}
