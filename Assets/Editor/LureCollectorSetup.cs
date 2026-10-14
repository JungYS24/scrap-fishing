using System.Text;
using ScrapFishing.Boat;
using ScrapFishing.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LureCollectorSetup
{
    const string ScenePath = "Assets/Scenes/Boat.unity";

    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var lure = Object.FindFirstObjectByType<HookMover>(FindObjectsInactive.Include);
        if (lure == null)
        {
            LogCandidates();
            Debug.LogError("보트 씬에서 Lure(HookMover)를 찾지 못해 배선을 중단합니다.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        var collector = lure.GetComponent<LureCollector>();
        if (collector == null)
        {
            collector = lure.gameObject.AddComponent<LureCollector>();
        }

        var settle = GameObject.Find("SettlePoint");
        if (settle == null)
        {
            var surface = GameObject.Find("SurfaceProps");
            settle = new GameObject("SettlePoint");
            if (surface != null)
            {
                settle.transform.SetParent(surface.transform, true);
            }

            settle.transform.position = new Vector3(0f, 0.85f, 0f);
        }

        var so = new SerializedObject(collector);
        var point = so.FindProperty("settlePoint");
        if (point != null)
        {
            point.objectReferenceValue = settle.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            collector.SetSettlePoint(settle.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LureCollector 배선 완료: " + lure.gameObject.name + " / SettlePoint " + settle.transform.position);
    }

    static void LogCandidates()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var names = new StringBuilder();
        for (var i = 0; i < roots.Length; i++)
        {
            if (i > 0)
            {
                names.Append(", ");
            }

            names.Append(roots[i].name);
        }

        Debug.Log("씬 루트 후보: " + names);
        var movers = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var lureLike = new StringBuilder();
        for (var i = 0; i < movers.Length; i++)
        {
            var name = movers[i].name;
            if (name.IndexOf("Lure", System.StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Hook", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (lureLike.Length > 0)
            {
                lureLike.Append(", ");
            }

            lureLike.Append(name);
        }

        Debug.Log("이름 후보: " + lureLike);
    }
}
