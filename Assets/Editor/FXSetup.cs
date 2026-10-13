using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FXSetup
{
    const string ScenePath = "Assets/Scenes/Boat.unity";

    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var roots = scene.GetRootGameObjects();
            var names = new StringBuilder();
            for (var i = 0; i < roots.Length; i++)
            {
                if (i > 0)
                {
                    names.Append(", ");
                }

                names.Append(roots[i].name);
            }

            Debug.LogError("Canvas를 찾지 못해 FX 배선을 중단합니다. 루트: " + names);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        var layer = canvas.transform.Find("FXLayer") as RectTransform;
        if (layer == null)
        {
            var go = new GameObject("FXLayer", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            layer = (RectTransform)go.transform;
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            go.transform.SetAsLastSibling();
        }

        EnsureImage(layer, "FXFade", Color.clear);
        EnsureImage(layer, "FXVignette", new Color(0.7f, 0.05f, 0.12f, 0f));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("FXSetup 완료: FXLayer on " + canvas.name);
    }

    static void EnsureImage(RectTransform parent, string name, Color color)
    {
        var child = parent.Find(name);
        Image image;
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image = go.AddComponent<Image>();
        }
        else
        {
            image = child.GetComponent<Image>();
            if (image == null)
            {
                image = child.gameObject.AddComponent<Image>();
            }
        }

        image.color = color;
        image.raycastTarget = false;
        if (child == null || child.GetComponent<CanvasGroup>() == null)
        {
            (child != null ? child.gameObject : image.gameObject).AddComponent<CanvasGroup>();
        }
    }
}
