using System;
using System.Reflection;
using DG.Tweening;
using ScrapFishing.FX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FxPunchRestTest
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("PunchRest", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        var original = new Vector3(1.7f, 0.4f, 1f);
        rect.localScale = original;
        rect.anchoredPosition = new Vector2(12f, -8f);

        var failed = false;
        try
        {
            for (var i = 0; i < 30; i++)
            {
                Fx.Punch(rect, 0.2f, 0.2f);
                Step(0.02f);
            }

            for (var i = 0; i < 20; i++)
            {
                Step(0.02f);
            }

            var scale = rect.localScale;
            if ((scale - original).sqrMagnitude > 0.0001f)
            {
                Debug.LogError("FxPunchRestTest FAIL scale=" + scale + " expected=" + original);
                failed = true;
            }
            else
            {
                Debug.Log("FxPunchRestTest PASS scale=" + scale);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("FxPunchRestTest FAIL " + ex);
            failed = true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }

    static void Step(float dt)
    {
        DOTween.ManualUpdate(dt, dt);
        var assembly = typeof(DOTween).Assembly;
        var manager = assembly.GetType("DG.Tweening.Core.TweenManager");
        var updateType = assembly.GetType("DG.Tweening.UpdateType");
        MethodInfo update = null;
        foreach (var method in manager.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.Name != "Update")
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 3 && parameters[0].ParameterType == updateType)
            {
                update = method;
                break;
            }
        }

        if (update == null)
        {
            throw new MissingMethodException("TweenManager.Update");
        }

        var normal = Enum.Parse(updateType, "Normal");
        update.Invoke(null, new object[] { normal, dt, dt });
    }
}
