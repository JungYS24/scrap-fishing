using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace ScrapFishing.Tests
{
    public class WebGLTextureSizeTests
    {
        // 규칙에 적은 파일이 실제로 있고, WebGL 최대 크기가 규칙대로 들어가 있으며, 다른 플랫폼 설정은 그대로다.
        [Test]
        public void Rules_AppliedToWebGLOnly()
        {
            foreach (var pair in WebGLTextureSizeImportPostprocessor.WebGLMaxSizes)
            {
                Assert.IsTrue(File.Exists(pair.Key), "규칙의 파일이 없음: " + pair.Key);
                var importer = (TextureImporter)AssetImporter.GetAtPath(pair.Key);
                var webgl = importer.GetPlatformTextureSettings("WebGL");
                Assert.IsTrue(webgl.overridden, pair.Key);
                Assert.AreEqual(pair.Value, webgl.maxTextureSize, pair.Key);
                Assert.AreEqual(TextureImporterFormat.RGBA32, webgl.format, "도트 그림은 무압축(Brotli에서 DXT보다 작음): " + pair.Key);
                Assert.GreaterOrEqual(importer.maxTextureSize, pair.Value, "기본(에디터·다른 플랫폼) 크기는 줄이지 않음: " + pair.Key);
            }
        }

        [Test]
        public void Rules_DoNotOverlapFolderPostprocessors()
        {
            foreach (var path in WebGLTextureSizeImportPostprocessor.WebGLMaxSizes.Keys)
            {
                StringAssert.DoesNotStartWith(OripaUiImportPostprocessor.Root, path);
                StringAssert.DoesNotStartWith(SettingsUiImportPostprocessor.Root, path);
                StringAssert.DoesNotStartWith(TutorialUiImportPostprocessor.Root, path);
                StringAssert.DoesNotStartWith(HudUiImportPostprocessor.Root, path);
                StringAssert.DoesNotStartWith("Assets/Art/DeepSee/", path);
            }
        }
    }
}
