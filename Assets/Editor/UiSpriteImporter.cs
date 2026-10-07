using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.Build
{
    // Assets/Resources/UI 아래 PNG를 UI 스프라이트로 자동 임포트한다.
    // 원본은 게임 해상도(480폭)의 약 3배라 PPU 300으로 두면 Set Native Size가 캔버스 기준 크기가 된다.
    public class UiSpriteImporter : AssetPostprocessor
    {
        const string Root = "Assets/Resources/UI/";
        const float PixelsPerUnit = 300f;

        // 9-slice 테두리(px): 왼쪽, 아래, 오른쪽, 위
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "frame_button_teal", new Vector4(70, 60, 70, 60) },
            { "frame_bar_teal", new Vector4(40, 50, 40, 50) },
            { "frame_panel_dim", new Vector4(28, 22, 28, 22) },
            { "frame_tile_teal", new Vector4(70, 70, 70, 70) },
            { "frame_tile_blue", new Vector4(60, 60, 60, 60) },
            { "frame_small_magenta", new Vector4(40, 40, 40, 40) },
            { "frame_small_cyan", new Vector4(40, 40, 40, 40) },
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;

            var name = Path.GetFileNameWithoutExtension(assetPath);
            importer.spriteBorder = Borders.TryGetValue(name, out var border) ? border : Vector4.zero;
        }
    }
}
