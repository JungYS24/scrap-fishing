using UnityEditor;
using UnityEngine;

// Resources/Gear 아래 장비 PNG(아이콘·보트/피셔 레이어·낚싯대)를 픽셀 좌표(anchors JSON)를 그대로 쓸 수 있게 가져온다.
// Sprite Single, Filter Point, Wrap Clamp, 압축 없음, PPU 100, 피벗 중앙. Multiple로 잘리면 기준점 좌표가 어긋나므로 반드시 Single이다.
public class GearImportPostprocessor : AssetPostprocessor
{
    public const string GearRoot = "Assets/Resources/Gear/";

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(GearRoot) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Apply((TextureImporter)assetImporter);
    }

    static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100f;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    // 이미 들어와 있던 파일에도 위 설정을 한 번에 적용한다.
    [MenuItem("Scrap Fishing/Reimport Gear Sprites")]
    public static void ReimportAll()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GearRoot.TrimEnd('/') });
        var count = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        Debug.Log("Gear 스프라이트 재가져오기: " + count + "장");
    }
}
