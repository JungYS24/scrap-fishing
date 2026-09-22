using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class PortraitGameViewSize
{
    static PortraitGameViewSize()
    {
        EditorApplication.delayCall += EnsureSize;
    }

    static void EnsureSize()
    {
        try
        {
            const int width = 480;
            const int height = 854;
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
            var total = (int)getBuiltinCount.Invoke(group, null) + (int)getCustomCount.Invoke(group, null);
            for (var i = 0; i < total; i++)
            {
                var size = getGameViewSize.Invoke(group, new object[] { i });
                var sizeType = size.GetType();
                var existingWidth = (int)sizeType.GetProperty("width").GetValue(size, null);
                var existingHeight = (int)sizeType.GetProperty("height").GetValue(size, null);
                if (existingWidth == width && existingHeight == height)
                {
                    return;
                }
            }

            var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            var ctor = gameViewSizeType.GetConstructor(new[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            var fixedResolution = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");
            var sizeObj = ctor.Invoke(new object[] { fixedResolution, width, height, "480x854 Portrait" });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { sizeObj });
        }
        catch (Exception)
        {
        }
    }
}
