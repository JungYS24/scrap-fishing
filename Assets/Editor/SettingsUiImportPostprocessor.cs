using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Assets/scrap_fishing_ui/settings 아래 설정 패널 스킨 PNG를 가져온다.
// Sprite Single, PPU 100, Full Rect, Bilinear(AI 업스케일 원본이라 Point면 축소 시 지글거림), 압축 없음(기본·WebGL), 밉맵 끔, Clamp.
// 9-slice 테두리는 기획 표의 [L,T,R,B] 픽셀 값이고, Unity spriteBorder는 (L,B,R,T) 순서라서 바꿔 넣는다.
public class SettingsUiImportPostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/scrap_fishing_ui/settings/";

    // 파일 이름 → [Left, Top, Right, Bottom]. 없는 파일(닫기·음소거·손잡이)은 테두리 없이 Simple로 쓴다.
    public static readonly Dictionary<string, int[]> BordersLTRB = new Dictionary<string, int[]>
    {
        { "panel", new[] { 130, 130, 130, 130 } },
        { "header_bar", new[] { 16, 16, 16, 16 } },
        { "label_box", new[] { 8, 8, 8, 8 } },
        { "slider_track", new[] { 10, 8, 10, 8 } },
        { "slider_fill", new[] { 3, 3, 3, 3 } },
        { "btn_wide_cyan", new[] { 20, 20, 20, 20 } },
        { "btn_wide_gray", new[] { 20, 20, 20, 20 } },
        { "btn_wide_yellow", new[] { 20, 20, 20, 20 } },
        { "btn_wide_green", new[] { 20, 20, 20, 20 } },
        { "footer_box", new[] { 8, 8, 8, 8 } },
    };

    public static Vector4 UnityBorder(int[] ltrb)
    {
        return new Vector4(ltrb[0], ltrb[3], ltrb[2], ltrb[1]);
    }

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);

        importer.spriteBorder = BordersLTRB.TryGetValue(Path.GetFileNameWithoutExtension(path), out var ltrb) ? UnityBorder(ltrb) : Vector4.zero;

        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "WebGL",
            overridden = true,
            maxTextureSize = 1024,
            format = TextureImporterFormat.RGBA32,
            textureCompression = TextureImporterCompression.Uncompressed,
        });
    }
}
