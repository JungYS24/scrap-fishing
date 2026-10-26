using UnityEditor;
using UnityEngine;

// Assets/scrap_fishing_ui/hud 아래 인게임 HUD 스킨 PNG를 설정 패널 스킨과 같은 설정으로 가져온다.
// 9-slice는 scrap_bar_fill만(1px). 나머지는 Simple + Preserve Aspect로 줄여 쓴다.
public class HudUiImportPostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/scrap_fishing_ui/hud/";
    public const string SlicedFile = "scrap_bar_fill";

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var border = System.IO.Path.GetFileNameWithoutExtension(path) == SlicedFile ? Vector4.one : Vector4.zero;
        SettingsUiImportPostprocessor.ApplyUiSprite((TextureImporter)assetImporter, border);
    }
}
