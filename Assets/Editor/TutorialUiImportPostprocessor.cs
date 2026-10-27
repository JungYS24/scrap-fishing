using System.Collections.Generic;
using System.IO;
using ScrapFishing.EditorTools;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEngine;

// Assets/scrap_fishing_ui/tutorial 아래 튜토리얼 스킨 PNG를 다른 UI 스킨과 같은 설정으로 가져온다.
// 9-slice는 tutorial_ui_manifest.json의 border_LTRB([L,T,R,B] 픽셀)를 Unity 순서(L,B,R,T)로 바꿔 넣고, null이면 Simple로 둔다.
// 가져온 뒤에는 Resources/UI/TutorialSkin.asset을 다시 채운다.
public class TutorialUiImportPostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/scrap_fishing_ui/tutorial/";
    public const string ManifestFile = "tutorial_ui_manifest.json";

    static bool _skinQueued;

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SettingsUiImportPostprocessor.ApplyUiSprite((TextureImporter)assetImporter, Border(Path.GetFileNameWithoutExtension(path)));
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (_skinQueued)
        {
            return;
        }

        foreach (var path in imported)
        {
            if (path.Replace('\\', '/').StartsWith(Root))
            {
                _skinQueued = true;
                EditorApplication.delayCall += () =>
                {
                    _skinQueued = false;
                    TutorialSkinMenu.Fill(false);
                };
                return;
            }
        }
    }

    // 매니페스트는 PNG보다 늦게 가져와질 수 있어 TextAsset 대신 파일을 바로 읽는다.
    public static Vector4 Border(string name)
    {
        var file = Root + ManifestFile;
        if (!File.Exists(file))
        {
            return Vector4.zero;
        }

        if (!(MiniJson.Parse(File.ReadAllText(file)) is Dictionary<string, object> root)
            || !root.TryGetValue(name, out var entryObject)
            || !(entryObject is Dictionary<string, object> entry)
            || !entry.TryGetValue("border_LTRB", out var borderObject)
            || !(borderObject is List<object> ltrb)
            || ltrb.Count != 4)
        {
            return Vector4.zero;
        }

        var l = System.Convert.ToSingle(ltrb[0]);
        var t = System.Convert.ToSingle(ltrb[1]);
        var r = System.Convert.ToSingle(ltrb[2]);
        var b = System.Convert.ToSingle(ltrb[3]);
        return new Vector4(l, b, r, t);
    }
}
