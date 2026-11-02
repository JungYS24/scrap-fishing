using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.Build
{
    // itch.io 빌드. GitHub Pages 템플릿(ScrapFishing)에 ITCHIO 심볼(CRAZYGAMES 끔)로 Builds/ItchIo에 굽고,
    // 업로드용 Builds/ItchIo.zip(최상위에 index.html·Build/·StreamingAssets/)을 만든다.
    // itch.io는 Content-Encoding 헤더를 붙여 주지 않으므로 압축은 Gzip + 압축 풀기 폴백(.unityweb)이다.
    // 빌드가 끝나면(성공·실패 모두) 템플릿·심볼·압축 등 PlayerSettings를 원래 값으로 되돌린다(BuildSettingsScope).
    public static class ItchIoBuilder
    {
        public const string Define = "ITCHIO";
        public const int MaxZipFiles = 1000;
        public const long MaxFileBytes = 200L * 1024 * 1024;

        // itch.io가 HTML5 게임을 iframe으로 띄우는 호스트. CrazyGames SDK 사이트락은 호스트가 정확히 맞아야 통과한다.
        public static readonly string[] Domains =
        {
            "https://html-classic.itch.zone",
            "https://html.itch.zone",
            "https://v6p9d9t4.ssl.hwcdn.net"
        };

        public static string OutputFolder => Path.GetFullPath(Path.Combine("Builds", "ItchIo"));
        public static string ZipPath => Path.GetFullPath(Path.Combine("Builds", "ItchIo.zip"));

        [MenuItem("Build/itch.io WebGL", false, 4)]
        public static void Build()
        {
            if (!GitHubPagesSiteLock.AreWhitelisted(Domains, "itch.io", out var problem))
            {
                WebGLBuilder.Fail("itch.io build stopped: " + problem);
                return;
            }

            bool ok;
            using (new BuildSettingsScope(WebGLBuilder.Template, CrazyGamesBuilder.DefinesFor(Define), WebGLCompressionFormat.Gzip, true))
            {
                ok = WebGLBuilder.BuildTo(OutputFolder);
            }

            if (!ok)
            {
                WebGLBuilder.Fail("itch.io build failed. See the log above.");
                return;
            }

            MakeZip(OutputFolder, ZipPath);
            if (!CheckZip(ZipPath, out problem))
            {
                WebGLBuilder.Fail("itch.io zip check failed: " + problem);
                return;
            }

            Debug.Log("itch.io build succeeded: " + ZipPath);
        }

        // folder 안의 파일을 폴더 한 겹 감싸지 않고 zip 최상위에 넣는다.
        public static void MakeZip(string folder, string zipPath)
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            var root = Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var name = file.Substring(root.Length).Replace('\\', '/');
                    using (var source = File.OpenRead(file))
                    using (var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal).Open())
                    {
                        source.CopyTo(entry);
                    }
                }
            }
        }

        // itch.io 업로드 조건: 최상위에 index.html·Build/·StreamingAssets/, 파일 1000개 이하, 파일마다 200MB 이하.
        public static bool CheckZip(string zipPath, out string problem)
        {
            if (!File.Exists(zipPath))
            {
                problem = "zip이 없습니다: " + zipPath;
                return false;
            }

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var files = zip.Entries.Where(e => e.Name.Length > 0).ToArray();
                var biggest = files.OrderByDescending(e => e.Length).FirstOrDefault();
                Debug.Log("itch.io zip: " + files.Length + " files (max " + MaxZipFiles + "), biggest "
                          + (biggest == null ? "-" : biggest.FullName + " " + (biggest.Length / 1024f / 1024f).ToString("0.00") + "MB")
                          + " (max " + MaxFileBytes / 1024 / 1024 + "MB)");

                if (!files.Any(e => e.FullName == "index.html"))
                {
                    problem = "zip 최상위에 index.html이 없습니다(폴더로 한 겹 감싸면 itch.io가 못 엽니다).";
                    return false;
                }

                foreach (var folder in new[] { "Build/", "StreamingAssets/" })
                {
                    if (!files.Any(e => e.FullName.StartsWith(folder, StringComparison.Ordinal)))
                    {
                        problem = "zip 최상위에 " + folder + "가 없습니다.";
                        return false;
                    }
                }

                if (files.Length > MaxZipFiles)
                {
                    problem = "파일이 " + files.Length + "개로 itch.io 한도 " + MaxZipFiles + "개를 넘습니다.";
                    return false;
                }

                var tooBig = files.FirstOrDefault(e => e.Length > MaxFileBytes);
                if (tooBig != null)
                {
                    problem = tooBig.FullName + "가 " + tooBig.Length / 1024 / 1024 + "MB로 itch.io 한도 200MB를 넘습니다.";
                    return false;
                }
            }

            problem = null;
            return true;
        }
    }
}
