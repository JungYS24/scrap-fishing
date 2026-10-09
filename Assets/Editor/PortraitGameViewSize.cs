using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class PortraitGameViewSize
{
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
    }

    static void EnsureSize()
    {
        try
        {
            var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singletonType.GetProperty("instance")?.GetValue(null, null);
            if (instance == null)
            {
                return;
            }

            var getGroup = sizesType.GetMethod("GetGroup");
            var group = getGroup.Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            var getBuiltinCount = groupType.GetMethod("GetBuiltinCount");
            var getCustomCount = groupType.GetMethod("GetCustomCount");
            var getGameViewSize = groupType.GetMethod("GetGameViewSize");
            var addCustomSize = groupType.GetMethod("AddCustomSize");
            var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            var ctor = gameViewSizeType.GetConstructor(new[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            var fixedResolution = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");

            foreach (var preset in Sizes)
            {
                if (HasSize(group, getBuiltinCount, getCustomCount, getGameViewSize, preset.Width, preset.Height))
                {
                    continue;
                }

                var sizeObj = ctor.Invoke(new object[] { fixedResolution, preset.Width, preset.Height, preset.Name });
                addCustomSize.Invoke(group, new[] { sizeObj });
            }
        }
        catch (Exception)
        {
        }
    }

    static bool HasSize(object group, MethodInfo getBuiltinCount, MethodInfo getCustomCount, MethodInfo getGameViewSize, int width, int height)
    {
        var total = (int)getBuiltinCount.Invoke(group, null) + (int)getCustomCount.Invoke(group, null);
        for (var i = 0; i < total; i++)
        {
            var size = getGameViewSize.Invoke(group, new object[] { i });
            var sizeType = size.GetType();
            var existingWidth = (int)sizeType.GetProperty("width").GetValue(size, null);
            var existingHeight = (int)sizeType.GetProperty("height").GetValue(size, null);
            if (existingWidth == width && existingHeight == height)
            {
                return true;
            }
        }

        return false;
    }
}
