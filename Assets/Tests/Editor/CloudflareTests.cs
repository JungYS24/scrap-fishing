using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScrapFishing.Build;
using UnityEditor;

namespace ScrapFishing.Tests
{
    public class CloudflareTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sf-cloudflare-check");
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }

            Directory.CreateDirectory(Path.Combine(_dir, "Build"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Test]
        public void WorkerHost_MatchesOnlyThisWorker()
        {
            Assert.IsTrue(CloudflareBuilder.IsWorkerHost("scrap-fishing.jungys24.workers.dev"));
            Assert.IsTrue(CloudflareBuilder.IsWorkerHost("Scrap-Fishing.Someone.Workers.Dev"));
            Assert.IsFalse(CloudflareBuilder.IsWorkerHost("scrap-fishing.workers.dev"), "계정 부분이 없음");
            Assert.IsFalse(CloudflareBuilder.IsWorkerHost("other.jungys24.workers.dev"));
            Assert.IsFalse(CloudflareBuilder.IsWorkerHost("scrap-fishing.jungys24.workers.dev.evil.com"));
            Assert.IsFalse(CloudflareBuilder.IsWorkerHost("jungys24.github.io"));
            Assert.AreEqual("scrap-fishing.a.workers.dev", GitHubPagesSiteLock.HostOf("https://scrap-fishing.a.workers.dev/"));
            Assert.AreEqual("scrap-fishing.a.workers.dev", GitHubPagesSiteLock.HostOf(" scrap-fishing.a.workers.dev "));
        }

        // SDK 사이트락은 모든 WebGL 빌드에서 돈다. workers.dev 주소가 허용 목록에 있어야 Cloudflare 판이 멈추지 않는다.
        [Test]
        public void SiteLock_WhitelistsWorker()
        {
            Assert.IsTrue(CloudflareBuilder.IsWhitelisted(out var host, out var problem), problem);
            StringAssert.EndsWith(".workers.dev", host);
        }

        // wrangler.jsonc는 저장소 루트에 있고, 이름·올릴 폴더가 빌더와 같으며, 호환 날짜가 오늘 이전이어야 한다.
        [Test]
        public void WranglerConfig_MatchesBuilder()
        {
            Assert.IsTrue(File.Exists(CloudflareBuilder.WranglerConfig), "저장소 루트에 wrangler.jsonc (.jsonc.txt가 아님)");
            var text = Regex.Replace(File.ReadAllText(CloudflareBuilder.WranglerConfig), @"^\s*//.*$", string.Empty, RegexOptions.Multiline);

            Assert.AreEqual(CloudflareBuilder.WorkerName, Value(text, "name"));
            Assert.IsTrue(Regex.IsMatch(CloudflareBuilder.WorkerName, "^[a-z0-9-]+$"), "Worker 이름은 영문 소문자·숫자·-");
            Assert.AreEqual("./" + CloudflareBuilder.WebFolderName, Value(text, "directory"));

            var date = DateTime.ParseExact(Value(text, "compatibility_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.LessOrEqual(date, DateTime.Today, "compatibility_date는 오늘 이전 날짜");
        }

        [Test]
        public void BuildScope_BrotliWithFallbackAndRestores()
        {
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            using (new BuildSettingsScope(WebGLBuilder.Template, CrazyGamesBuilder.DefinesWith(false), WebGLCompressionFormat.Brotli, true))
            {
                Assert.AreEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);
                Assert.IsTrue(PlayerSettings.WebGL.decompressionFallback);
            }

            Assert.AreEqual(compression, PlayerSettings.WebGL.compressionFormat);
            Assert.AreEqual(fallback, PlayerSettings.WebGL.decompressionFallback);
        }

        [Test]
        public void OutputCheck_PassesBrotliFallbackBuild()
        {
            WriteBuild();
            Assert.IsTrue(CloudflareBuilder.ValidateOutput(_dir, out var problem), problem);
        }

        [Test]
        public void OutputCheck_FailsWithoutFallbackFiles()
        {
            WriteBuild();
            File.Delete(Path.Combine(_dir, "Build", "Cloudflare.wasm.unityweb"));
            File.WriteAllBytes(Path.Combine(_dir, "Build", "Cloudflare.wasm.br"), new byte[] { 1, 2, 3 });
            Assert.IsFalse(CloudflareBuilder.ValidateOutput(_dir, out var problem));
            StringAssert.Contains("wasm.unityweb", problem);
        }

        [Test]
        public void OutputCheck_FailsOnRawBrFile()
        {
            WriteBuild();
            File.WriteAllBytes(Path.Combine(_dir, "Build", "Cloudflare.data.br"), new byte[] { 1, 2, 3 });
            Assert.IsFalse(CloudflareBuilder.ValidateOutput(_dir, out var problem));
            StringAssert.Contains(".br", problem);
        }

        [Test]
        public void OutputCheck_FailsOnGzip()
        {
            WriteBuild();
            File.WriteAllBytes(Path.Combine(_dir, "Build", "Cloudflare.data.unityweb"), new byte[] { 0x1F, 0x8B, 8, 0 });
            Assert.IsFalse(CloudflareBuilder.ValidateOutput(_dir, out var problem));
            StringAssert.Contains("Gzip", problem);
        }

        [Test]
        public void OutputCheck_FailsOverTwentyFiveMiB()
        {
            WriteBuild();
            using (var big = File.Create(Path.Combine(_dir, "StreamingAssets.bin")))
            {
                big.SetLength(CloudflareBuilder.MaxFileBytes + 1);
            }

            Assert.IsFalse(CloudflareBuilder.ValidateOutput(_dir, out var problem));
            StringAssert.Contains("25MiB", problem);
        }

        [Test]
        public void Publish_ForCloudflareSkipsNoJekyll()
        {
            WriteBuild();
            File.WriteAllText(Path.Combine(_dir, "index.html"), "x");
            var web = _dir + "-web";
            try
            {
                WebGLBuilder.PublishStaging(_dir, web, false);
                Assert.IsTrue(File.Exists(Path.Combine(web, "Build", "Cloudflare.wasm.unityweb")));
                Assert.IsTrue(File.Exists(Path.Combine(web, "index.html")));
                Assert.IsFalse(File.Exists(Path.Combine(web, ".nojekyll")));
            }
            finally
            {
                if (Directory.Exists(web))
                {
                    Directory.Delete(web, true);
                }
            }
        }

        void WriteBuild()
        {
            var build = Path.Combine(_dir, "Build");
            File.WriteAllText(Path.Combine(build, "Cloudflare.loader.js"), "x");
            foreach (var name in new[] { "Cloudflare.data.unityweb", "Cloudflare.wasm.unityweb", "Cloudflare.framework.js.unityweb" })
            {
                // Brotli 스트림에는 정해진 머리가 없다. gzip(1F 8B)이 아니면 된다.
                File.WriteAllBytes(Path.Combine(build, name), new byte[] { 0x1B, 0x2A, 0x00, 0x00 });
            }
        }

        static string Value(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            Assert.IsTrue(match.Success, "wrangler.jsonc에 " + key + "가 없음");
            return match.Groups[1].Value;
        }
    }
}
