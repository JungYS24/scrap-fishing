using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 세로 Game 뷰 크기를 등록하고, 에디터를 켤 때(세션마다 한 번) 보트 씬을 열고 Game 뷰를 480×854로 맞춘다.
// 크기는 Standalone과 지금 빌드 대상(WebGL 등)의 Game 뷰 크기 목록 모두에 넣는다.
[InitializeOnLoad]
internal static class PortraitGameViewSize
{
    public const string BootScene = "Assets/Scenes/Boat.unity";
    public const int DefaultWidth = 480;
    public const int DefaultHeight = 854;
    const string StartupKey = "ScrapFishing.EditorStartupDone";

    static readonly (int Width, int Height, string Name)[] Sizes =
    {
        (480, 854, "480x854 Portrait"),
        (1080, 1920, "1080x1920"),
        (1080, 2400, "1080x2400 Galaxy"),
        (1170, 2532, "1170x2532 iPhone")
    };

    static PortraitGameViewSize()
    {
        EditorApplication.delayCall += EnsureSize;
        if (!SessionState.GetBool(StartupKey, false))
        {
            SessionState.SetBool(StartupKey, true);
            EditorApplication.delayCall += OnEditorStartup;
        }
    }

    // 에디터를 켰을 때 한 번: 보트 씬을 열고(저장 안 한 변경이 있으면 그대로 둔다) Game 뷰를 480×854로.
    static void OnEditorStartup()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        OpenBootScene();
        SelectDefaultSize();
    }

    static void OpenBootScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == BootScene)
        {
            return;
        }

        for (var i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                Debug.Log("저장하지 않은 씬이 있어 보트 씬으로 바꾸지 않았습니다.");
                return;
            }
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene) != null)
        {
            EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
        }
    }

    [MenuItem("Tools/Scrap Fishing/Game View 480x854")]
    public static void SelectDefaultSize()
    {
        try
        {
            EnsureSize();
            var group = CurrentGroup();
            if (group == null)
            {
                return;
            }

            var index = IndexOf(group, DefaultWidth, DefaultHeight);
            if (index < 0)
            {
                return;
            }

            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var selected = gameViewType.GetProperty("selectedSizeIndex", flags);
            var callback = gameViewType.GetMethod("SizeSelectionCallback", flags);
            foreach (var view in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                if (callback != null)
                {
                    callback.Invoke(view, new object[] { index, null });
                }
                else if (selected != null && selected.CanWrite)
                {
                    selected.SetValue(view, index, null);
                }

                ((EditorWindow)view).Repaint();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Game 뷰를 480x854로 맞추지 못했습니다: " + exception.Message);
        }
    }

    public static void EnsureSize()
    {
        try
        {
            var sizesType = SizesType();
            var instance = Instance(sizesType);
            if (instance == null)
            {
                return;
            }

            var getGroup = sizesType.GetMethod("GetGroup");
            AddSizes(getGroup.Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone }));
            var current = CurrentGroup();
            if (current != null)
            {
                AddSizes(current);
            }
        }
        catch (Exception)
        {
        }
    }

    static Type SizesType()
    {
        return typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
    }

    static object Instance(Type sizesType)
    {
        var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        return singletonType.GetProperty("instance")?.GetValue(null, null);
    }

    // 지금 빌드 대상의 Game 뷰 크기 목록(Game 뷰 드롭다운에 보이는 목록).
    public static object CurrentGroup()
    {
        var sizesType = SizesType();
        var instance = Instance(sizesType);
        return instance != null ? sizesType.GetProperty("currentGroup")?.GetValue(instance, null) : null;
    }

    static void AddSizes(object group)
    {
        var groupType = group.GetType();
        var addCustomSize = groupType.GetMethod("AddCustomSize");
        var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
        var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
        var ctor = gameViewSizeType.GetConstructor(new[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
        var fixedResolution = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");
        foreach (var preset in Sizes)
        {
            if (IndexOf(group, preset.Width, preset.Height) >= 0)
            {
                continue;
            }

            var sizeObj = ctor.Invoke(new object[] { fixedResolution, preset.Width, preset.Height, preset.Name });
            addCustomSize.Invoke(group, new[] { sizeObj });
        }
    }

    public static int IndexOf(object group, int width, int height)
    {
        var groupType = group.GetType();
        var total = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null) + (int)groupType.GetMethod("GetCustomCount").Invoke(group, null);
        var getGameViewSize = groupType.GetMethod("GetGameViewSize");
        for (var i = 0; i < total; i++)
        {
            var size = getGameViewSize.Invoke(group, new object[] { i });
            var sizeType = size.GetType();
            var existingWidth = (int)sizeType.GetProperty("width").GetValue(size, null);
            var existingHeight = (int)sizeType.GetProperty("height").GetValue(size, null);
            if (existingWidth == width && existingHeight == height)
            {
                return i;
            }
        }

        return -1;
    }
}
