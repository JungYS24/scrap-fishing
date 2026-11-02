using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using ScrapFishing.Build;
using ScrapFishing.UI;
using UnityEditor;
using UnityEditor.Build;

namespace ScrapFishing.Tests
{
    public class ItchIoTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sf-itchio-test");
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }

            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        // SDK 사이트락은 iframe 주소의 호스트가 허용 목록과 정확히 맞아야 통과한다. itch.io 호스트 세 개가 모두 있어야 한다.
        [Test]
        public void SiteLock_WhitelistsItchHosts()
        {
            Assert.IsTrue(GitHubPagesSiteLock.AreWhitelisted(ItchIoBuilder.Domains, "itch.io", out var problem), problem);
            Assert.IsFalse(GitHubPagesSiteLock.AreWhitelisted(new[] { "https://example.itch.zone" }, "itch.io", out problem));
            StringAssert.Contains("example.itch.zone", problem);
        }

        [Test]
        public void Defines_TurnOnItchAndOffCrazyGames()
        {
            var itch = ";" + CrazyGamesBuilder.DefinesFor(ItchIoBuilder.Define) + ";";
            StringAssert.Contains(";" + ItchIoBuilder.Define + ";", itch);
            StringAssert.DoesNotContain(";" + CrazyGamesBuilder.Define + ";", itch);

            var crazy = ";" + CrazyGamesBuilder.DefinesWith(true) + ";";
            StringAssert.DoesNotContain(";" + ItchIoBuilder.Define + ";", crazy);
            StringAssert.DoesNotContain(";" + ItchIoBuilder.Define + ";", ";" + CrazyGamesBuilder.DefinesWith(false) + ";");
        }

        // 빌드 동안만 GitHub 템플릿 + ITCHIO + Gzip·폴백이고, 끝나면 원래 설정으로 돌아온다.
        [Test]
        public void BuildScope_UsesGzipFallbackAndRestores()
        {
            var template = PlayerSettings.WebGL.template;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            using (new BuildSettingsScope(WebGLBuilder.Template, CrazyGamesBuilder.DefinesFor(ItchIoBuilder.Define), WebGLCompressionFormat.Gzip, true))
            {
                Assert.AreEqual(WebGLBuilder.Template, PlayerSettings.WebGL.template);
                Assert.AreEqual(WebGLCompressionFormat.Gzip, PlayerSettings.WebGL.compressionFormat);
                Assert.IsTrue(PlayerSettings.WebGL.decompressionFallback);
                StringAssert.Contains(ItchIoBuilder.Define, PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL));
            }

            Assert.AreEqual(template, PlayerSettings.WebGL.template);
            Assert.AreEqual(defines, PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL));
            Assert.AreEqual(compression, PlayerSettings.WebGL.compressionFormat);
            Assert.AreEqual(fallback, PlayerSettings.WebGL.decompressionFallback);
        }

        [Test]
        public void Zip_PutsIndexBuildAndStreamingAssetsAtTop()
        {
            var folder = Path.Combine(_dir, "ItchIo");
            Write(folder, "index.html");
            Write(folder, "Build/ItchIo.loader.js");
            Write(folder, "Build/ItchIo.data.unityweb");
            Write(folder, "StreamingAssets/Gacha/Banners/a.jpg");
            var zipPath = Path.Combine(_dir, "ItchIo.zip");

            ItchIoBuilder.MakeZip(folder, zipPath);

            Assert.IsTrue(ItchIoBuilder.CheckZip(zipPath, out var problem), problem);
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                Assert.IsNotNull(zip.GetEntry("index.html"));
                Assert.IsNotNull(zip.GetEntry("StreamingAssets/Gacha/Banners/a.jpg"), "폴더 구분은 /");
                Assert.IsNull(zip.GetEntry("ItchIo/index.html"), "폴더로 한 겹 감싸지 않는다");
            }
        }

        [Test]
        public void Zip_FailsWhenWrappedInFolderOrMissingStreamingAssets()
        {
            var wrapped = Path.Combine(_dir, "wrapped");
            Write(wrapped, "ItchIo/index.html");
            Write(wrapped, "ItchIo/Build/a.js");
            Write(wrapped, "ItchIo/StreamingAssets/a.mp3");
            var zipPath = Path.Combine(_dir, "wrapped.zip");
            ItchIoBuilder.MakeZip(wrapped, zipPath);
            Assert.IsFalse(ItchIoBuilder.CheckZip(zipPath, out var problem));
            StringAssert.Contains("index.html", problem);

            var noAssets = Path.Combine(_dir, "noAssets");
            Write(noAssets, "index.html");
            Write(noAssets, "Build/a.js");
            zipPath = Path.Combine(_dir, "noAssets.zip");
            ItchIoBuilder.MakeZip(noAssets, zipPath);
            Assert.IsFalse(ItchIoBuilder.CheckZip(zipPath, out problem));
            StringAssert.Contains("StreamingAssets/", problem);
        }

        [Test]
        public void Zip_FailsOverFileLimit()
        {
            var folder = Path.Combine(_dir, "many");
            Write(folder, "index.html");
            Write(folder, "Build/a.js");
            for (var i = 0; i < ItchIoBuilder.MaxZipFiles; i++)
            {
                Write(folder, "StreamingAssets/f" + i + ".txt");
            }

            var zipPath = Path.Combine(_dir, "many.zip");
            ItchIoBuilder.MakeZip(folder, zipPath);
            Assert.IsFalse(ItchIoBuilder.CheckZip(zipPath, out var problem));
            StringAssert.Contains("1000", problem);
        }

        // itch.io 빌드(ITCHIO)에서도 테스트 쿠폰은 "올바르지 않은 쿠폰"이다. GitHub Pages 빌드(둘 다 꺼짐)에서만 쓸 수 있다.
        [Test]
        public void CheatCoupons_InvalidOnItchIo()
        {
#if ITCHIO
            Assert.IsFalse(SettingsView.CheatCouponsEnabled);
            Assert.AreEqual(0, SettingsView.CouponReward("smtm"));
            Assert.AreEqual(0, SettingsView.CouponReward("fish"));
            Assert.IsFalse(SettingsView.CheatCouponsEnabled && Core.SaveReset.IsResetCode("reset"));
#elif CRAZYGAMES
            Assert.IsFalse(SettingsView.CheatCouponsEnabled);
#else
            Assert.IsTrue(SettingsView.CheatCouponsEnabled);
#endif
        }

        static void Write(string folder, string relative)
        {
            var path = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
        }
    }
}
