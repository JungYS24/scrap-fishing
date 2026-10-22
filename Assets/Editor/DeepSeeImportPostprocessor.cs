using UnityEditor;
using UnityEngine;

public class DeepSeeImportPostprocessor : AssetPostprocessor
{
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
        webgl.maxTextureSize = 2048;
        webgl.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings(webgl);
    }
}
