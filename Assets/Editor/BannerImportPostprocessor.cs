using UnityEditor;
using UnityEngine;

// Resources/Gacha/Banners 아래 오리파 배너 아트(1024×768 JPG)를 가져온다.
// Sprite Single, PPU 100, Full Rect, Bilinear, 최대 1024, 밉맵 끔, Clamp. 압축은 WebGL 용량을 위해 기본 압축(Normal Quality).
public class BannerImportPostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/Resources/Gacha/Banners/";

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root))
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
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
    }
}
