using ScrapFishing.Gacha;
using ScrapFishing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Build
{
    // 타이틀의 오리파 버튼을 TitleView에 연결하고, 버전 표기와 암시장 화면 틀(보관함 포함)을 Boat 씬 하이러키에 만든다.
    // 타이틀 배치는 건드리지 않는다. BlackMarket도 이미 있는 오브젝트는 그대로 두고 빠진 것만 채운다.
    public static class OripaSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Boat.unity";
        const int UiLayer = 5;
        const string OripaPanelPath = "Assets/scrap_fishing_ui/panels/panel_oripa.png";

        [MenuItem("Scrap Fishing/Build Oripa UI")]
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

            var title = canvas.transform.Find("Title") as RectTransform;
            var view = title != null ? title.GetComponent<TitleView>() : null;
            if (view == null)
            {
                Debug.LogError("Title(TitleView)이 없습니다. 먼저 Scrap Fishing > Build Boat Scene을 실행하세요.");
                return;
            }

            var oripa = LinkOripaButton(title, view);
            EnsureVersionText(title);
            var market = BuildMarket((RectTransform)canvas.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = market.gameObject;
            Debug.Log("Oripa UI built: Title/" + oripa.name + " 연결, " + market.name);
        }

        // 타이틀 오리파 버튼의 패널 이미지를 panel_oripa로 바꾼다. 패널 자체 색을 그대로 보이게 틴트는 흰색으로 둔다.
        [MenuItem("Scrap Fishing/Apply Oripa Button Panel")]
        public static void ApplyOripaPanel()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var sprite = LoadPanelSprite();
            if (sprite == null)
            {
                Debug.LogError("패널 이미지를 찾지 못했습니다: " + OripaPanelPath);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindFirstObjectByType<BoatCanvas>();
            var title = canvas != null ? canvas.transform.Find("Title") as RectTransform : null;
            var button = TitleView.FindOripaButton(title);
            var image = button != null ? button.targetGraphic as Image : null;
            if (image == null)
            {
                Debug.LogError("Title/OripaButton의 Image가 없습니다.");
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            EditorUtility.SetDirty(image);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("OripaButton 패널 교체: " + sprite.name);
        }

        static Sprite LoadPanelSprite()
        {
            if (AssetImporter.GetAtPath(OripaPanelPath) is TextureImporter importer
                && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(OripaPanelPath);
        }

        // 하이러키의 Title/OripaButton을 TitleView.oripaButton에 연결한다. 없을 때만 출항 버튼 아래에 같은 크기로 만든다.
        public static Button LinkOripaButton(RectTransform title, TitleView view)
        {
            var so = new SerializedObject(view);
            var oripa = TitleView.FindOripaButton(title);
            if (oripa == null)
            {
                var start = so.FindProperty("startButton").objectReferenceValue as Button;
                oripa = TitleView.CreateMarketButton(title, start);
                oripa.onClick.RemoveAllListeners();
                SetLayer(oripa.transform, UiLayer);
            }

            so.FindProperty("oripaButton").objectReferenceValue = oripa;
            so.ApplyModifiedPropertiesWithoutUndo();
            return oripa;
        }

        // 이미 있으면 그대로 둔다(씬에서 옮긴 위치 유지).
        public static void EnsureVersionText(RectTransform title)
        {
            if (title.Find(TitleView.VersionTextName) != null)
            {
                return;
            }

            SetLayer(TitleView.CreateVersionText(title).transform, UiLayer);
        }

        static RectTransform BuildMarket(RectTransform canvas)
        {
            var host = new GameObject("OripaLayoutHost");
            try
            {
                host.AddComponent<GachaShopView>().BuildLayout(canvas);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            var root = (RectTransform)canvas.Find("BlackMarket");
            var fx = canvas.Find("FXLayer");
            if (fx != null)
            {
                root.SetSiblingIndex(fx.GetSiblingIndex());
            }

            SetLayer(root, UiLayer);
            root.gameObject.SetActive(false);
            return root;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
            }
        }
    }
}
