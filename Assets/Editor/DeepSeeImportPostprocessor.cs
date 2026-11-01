using UnityEditor;
using UnityEngine;

// 잠수 배경(480×1194). WebGL은 최대 1024로 줄여(412×1024) DXT1로 압축한다.
// 원본 높이 1194는 4의 배수가 아니라 원본 크기로는 DXT로 압축되지 않고 무압축(RGB24)으로 들어가 첫 다운로드가 장당 0.5~1MB 늘었다.
// 화면에서는 480×854 기준 1:1 정도(창 높이 720이면 0.84배)로 보이므로 1024로 줄여도 차이가 작다. 도트라 Point 필터는 그대로 둔다.
public class DeepSeeImportPostprocessor : AssetPostprocessor
{
    public const int WebGLMaxSize = 1024;

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Art/DeepSee/"))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.maxTextureSize = 2048;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spritePixelsPerUnit = 100f;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.alphaIsTransparency = true;
        importer.SetTextureSettings(settings);

        var webgl = importer.GetPlatformTextureSettings("WebGL");
        webgl.overridden = true;
        webgl.maxTextureSize = WebGLMaxSize;
        webgl.textureCompression = TextureImporterCompression.Compressed;
        webgl.format = TextureImporterFormat.DXT1;
        importer.SetPlatformTextureSettings(webgl);
    }
}
