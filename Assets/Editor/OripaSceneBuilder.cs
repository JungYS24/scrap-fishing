using ScrapFishing.Gacha;
using ScrapFishing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Build
{
    // 타이틀의 오리파 버튼을 TitleView에 연결하고, 암시장 화면 틀을 Boat 씬 하이러키에 만든다.
    // 타이틀 배치는 건드리지 않는다. BlackMarket도 이미 있는 오브젝트는 그대로 두고 빠진 것만 채운다.
    public static class OripaSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Boat.unity";
        const int UiLayer = 5;

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
            var market = BuildMarket((RectTransform)canvas.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = market.gameObject;
            Debug.Log("Oripa UI built: Title/" + oripa.name + " 연결, " + market.name);
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
