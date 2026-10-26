using System.Collections.Generic;
using System.IO;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEngine;

// Assets/scrap_fishing_ui/oripa 아래 오리파·보관함 UI 스킨 PNG를 설정 패널 스킨과 같은 설정으로 가져온다(최대 1024).
// 9-slice 테두리는 같은 폴더 oripa_ui_manifest.json의 border_LTRB([L,T,R,B])를 Unity 순서(L,B,R,T)로 바꿔 넣는다. null이면 테두리 없음.
public class OripaUiImportPostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/scrap_fishing_ui/oripa/";
    public const string Manifest = Root + "oripa_ui_manifest.json";

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        var ltrb = BorderOf(Path.GetFileNameWithoutExtension(path));
        SettingsUiImportPostprocessor.ApplyUiSprite(importer, ltrb != null ? SettingsUiImportPostprocessor.UnityBorder(ltrb) : Vector4.zero);
        importer.maxTextureSize = 1024;
    }

    // manifest의 [Left, Top, Right, Bottom]. 없거나 null이면 null.
    public static int[] BorderOf(string name)
    {
        if (!File.Exists(Manifest))
        {
            return null;
        }

        var root = MiniJson.Parse(File.ReadAllText(Manifest)) as Dictionary<string, object>;
        if (root == null || !(root.TryGetValue(name, out var entry) && entry is Dictionary<string, object> sprite)
            || !(sprite.TryGetValue("border_LTRB", out var value) && value is List<object> list) || list.Count != 4)
        {
            return null;
        }

        var border = new int[4];
        for (var i = 0; i < 4; i++)
        {
            border[i] = list[i] is double number ? (int)number : 0;
        }

        return border;
    }
}
