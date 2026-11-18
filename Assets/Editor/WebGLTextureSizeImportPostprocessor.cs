using System.Collections.Generic;
using UnityEditor;

// WebGL 빌드에서만 텍스처 최대 크기를 화면에 보이는 크기에 맞춰 줄인다(네트워크 프로그래밍 9강 실습 5).
// 폴더별 후처리기가 없는 파일만 여기서 정한다. 원본 이미지와 다른 플랫폼 설정은 바꾸지 않는다.
// 화면 기준은 480×854(CanvasScaler 기준 해상도, 카메라 1유닛 = 100px)이고, 웹은 기기 픽셀 비율 2까지 그리므로
// 화면에 보이는 크기의 2배 안쪽이면 흐려지지 않는다.
public class WebGLTextureSizeImportPostprocessor : AssetPostprocessor
{
    // 전송량은 압축 후 크기라서 압축 전 크기가 줄어도 늘 수 있다. 도트 그림을 정수배가 아닌 비율로 줄이면 중간색이 생겨
    // Brotli가 잘 못 줄인다. 그래서 줄인 뒤 압축 크기가 실제로 작아진 파일만 둔다(2026-11 측정, 원시 RGBA gzip 기준):
    //   panel_money 813×172 → 512×108: 93KB → 47KB (넣음)
    //   MidgroundFaded 960×512 → 512×273: 63KB → 104KB (늘어서 뺌)
    //   타이틀 로고 704×628 → 512×457: DXT로 들어가던 것이 4의 배수가 아니어서 무압축이 되며 .data가 늘어 뺌
    //   panel_oripa·panel_battle → 512: 68→60KB, 98→96KB (차이가 작아 뺌)
    public static readonly Dictionary<string, int> WebGLMaxSizes = new Dictionary<string, int>
    {
        // 돈 패널(813×172). 화면에서 약 160px 폭(기기 픽셀 비율 2에서 약 320px) → 512
        { "Assets/scrap_fishing_ui/panels/panel_money.png", 512 },
    };

    // 규칙을 바꾼 뒤 해당 텍스처만 다시 임포트한다(후처리기만 바뀌면 Unity가 저절로 다시 임포트하지 않는다).
    [MenuItem("Tools/Scrap Fishing/Reimport WebGL Texture Sizes")]
    public static void Reimport()
    {
        foreach (var path in WebGLMaxSizes.Keys)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (!WebGLMaxSizes.TryGetValue(path, out var max))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        var webgl = importer.GetPlatformTextureSettings("WebGL");
        webgl.overridden = true;
        webgl.maxTextureSize = max;
        // 무압축 RGBA32로 고정한다. 크기를 줄이면 가로·세로가 4의 배수가 되거나 안 되거나 하면서 DXT/무압축이 뒤바뀌는데,
        // 도트 그림은 무압축이 Brotli에서 DXT보다 훨씬 작게 줄어(오리파 UI와 같은 이유) 전송량이 오히려 늘 수 있다.
        webgl.textureCompression = TextureImporterCompression.Uncompressed;
        webgl.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(webgl);
    }
}
