using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.Build
{
    // Cloudflare Workers 빌드(네트워크 프로그래밍 9강). GitHub Pages와 같은 템플릿·심볼(CRAZYGAMES 끔)로
    // Builds/Cloudflare에 굽고, "Build/Publish Cloudflare to web"으로 저장소 루트 web/에 올린다.
    // 푸시하면 Cloudflare 빌드 서버가 npx wrangler deploy로 wrangler.jsonc의 assets.directory(./web)를 올린다.
    //
    // 압축은 Brotli + 압축 풀기 폴백(.unityweb)이다. Cloudflare는 응답을 한 번 더 압축하고 Content-Encoding을
    // 직접 붙일 수 없어서, 폴백 없이 .br을 올리면 브라우저가 바깥 겹만 풀어 framework.js에서 SyntaxError로 멈춘다.
    // 폴백이면 Unity 로더가 안쪽 Brotli를 직접 푼다. Workers 정적 파일은 한 개 25MiB까지라 그것도 검사한다.
    public static class CloudflareBuilder
    {
        public const string WorkerName = "scrap-fishing";
        public const string WebFolderName = "web";
        public const string WranglerConfig = "wrangler.jsonc";
        public const long MaxFileBytes = 25L * 1024 * 1024;
        public const int MaxFiles = 20000;

        static readonly string[] UnitywebSuffixes = { ".data.unityweb", ".wasm.unityweb", ".framework.js.unityweb" };

        public static string OutputFolder => Path.GetFullPath(Path.Combine("Builds", "Cloudflare"));
        public static string WebFolder => Path.GetFullPath(WebFolderName);

        // 주소는 scrap-fishing.<계정>.workers.dev. 계정 서브도메인은 Cloudflare 대시보드(Workers & Pages)에서 확인해
        // CrazyGamesSettings.asset의 whitelistedDomains에 https://scrap-fishing.<계정>.workers.dev로 넣는다.
        public static bool IsWorkerHost(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            var parts = host.ToLowerInvariant().Split('.');
            return parts.Length == 4 && parts[0] == WorkerName && parts[1].Length > 0 && parts[2] == "workers" && parts[3] == "dev";
        }

        public static bool IsWhitelisted(out string host, out string problem)
        {
            if (GitHubPagesSiteLock.FindHost(IsWorkerHost, out host, out problem))
            {
                return true;
            }

            if (problem == null)
            {
                problem = GitHubPagesSiteLock.SettingsPath + "의 whitelistedDomains에 https://" + WorkerName
                    + ".<계정>.workers.dev가 없습니다. 없으면 CrazyGames SDK 사이트락이 Cloudflare 판을 시작하자마자 멈춥니다."
                    + " Cloudflare 대시보드 Workers & Pages에서 계정 서브도메인을 확인해 넣어 주세요.";
            }

            return false;
        }

        // 실습 1: Brotli + Decompression Fallback 빌드. 사이트락 도메인이 아직 없어도 용량 확인용으로는 빌드하고 경고만 남긴다
        // (web/에 올리는 Publish 단계에서 막는다).
        [MenuItem("Build/Cloudflare WebGL", false, 5)]
        public static void Build()
        {
            if (!IsWhitelisted(out _, out var siteLockProblem))
            {
                Debug.LogWarning("Cloudflare build: " + siteLockProblem + " (빌드는 계속하지만 web/에 올리기 전에 고쳐야 합니다)");
            }

            bool ok;
            using (new BuildSettingsScope(WebGLBuilder.Template, CrazyGamesBuilder.DefinesWith(false), WebGLCompressionFormat.Brotli, true))
            {
                ok = WebGLBuilder.BuildTo(OutputFolder);
            }

            if (!ok)
            {
                WebGLBuilder.Fail("Cloudflare build failed. See the log above.");
                return;
            }

            if (!ValidateOutput(OutputFolder, out var problem))
            {
                WebGLBuilder.Fail("Cloudflare build check failed: " + problem);
                return;
            }

            Debug.Log("Cloudflare build succeeded: " + OutputFolder + "\n" + DescribeBuild(OutputFolder)
                      + "\n(publish with Build/Publish Cloudflare to web)");
        }

        // Builds/Cloudflare를 web/에 올린다. 커밋·푸시하면 Cloudflare가 배포한다.
        [MenuItem("Build/Publish Cloudflare to web", false, 6)]
        public static void PublishToWeb()
        {
            if (!IsWhitelisted(out var host, out var siteLockProblem))
            {
                WebGLBuilder.Fail("Cloudflare publish stopped: " + siteLockProblem);
                return;
            }

            if (!Directory.Exists(OutputFolder) || !WebGLBuilder.ValidateWebGlOutput(OutputFolder)
                || !ValidateOutput(OutputFolder, out var problem))
            {
                WebGLBuilder.Fail("Builds/Cloudflare has no valid build. Run Build/Cloudflare WebGL first.");
                return;
            }

            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Publish Cloudflare",
                    "Builds/Cloudflare 빌드로 web/을 덮어씁니다.\n커밋·푸시하면 https://" + host + " 에 배포됩니다. 계속할까요?",
                    "web에 복사",
                    "취소"))
            {
                return;
            }

            WebGLBuilder.PublishStaging(OutputFolder, WebFolder, false);
            Debug.Log("Published Cloudflare build to " + WebFolder + " (https://" + host + ")\n" + DescribeBuild(WebFolder));
        }

        // Cloudflare에 올릴 수 있는 빌드인지: Build/에 데이터·코드·프레임워크가 폴백 형식(.unityweb, gzip 아님 = Brotli)으로 있고
        // .br·.gz가 없으며, 모든 파일이 25MiB 이하이고 파일 수가 한도 안이어야 한다.
        public static bool ValidateOutput(string output, out string problem)
        {
            var buildDir = Path.Combine(output, "Build");
            if (!Directory.Exists(buildDir))
            {
                problem = "Build 폴더가 없습니다: " + buildDir;
                return false;
            }

            var names = Directory.GetFiles(buildDir).Select(Path.GetFileName).ToArray();
            foreach (var suffix in UnitywebSuffixes)
            {
                var name = names.FirstOrDefault(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                if (name == null)
                {
                    problem = "Build에 *" + suffix + "가 없습니다. Decompression Fallback이 켜져 있는지 확인하세요.";
                    return false;
                }

                if (IsGzip(Path.Combine(buildDir, name)))
                {
                    problem = name + "가 Gzip입니다. Compression Format을 Brotli로 빌드하세요.";
                    return false;
                }
            }

            var raw = names.FirstOrDefault(f => f.EndsWith(".br", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gz", StringComparison.OrdinalIgnoreCase));
            if (raw != null)
            {
                problem = raw + "가 있습니다. Cloudflare는 응답을 다시 압축해 .br/.gz를 그대로 두면 로딩이 멈춥니다(폴백 빌드로 다시 굽기).";
                return false;
            }

            var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
            if (files.Length > MaxFiles)
            {
                problem = "파일이 " + files.Length + "개로 Workers 정적 파일 한도 " + MaxFiles + "개를 넘습니다.";
                return false;
            }

            var tooBig = files.FirstOrDefault(f => new FileInfo(f).Length > MaxFileBytes);
            if (tooBig != null)
            {
                problem = tooBig + "가 " + Mb(new FileInfo(tooBig).Length) + "로 Workers 한도 25MiB를 넘습니다.";
                return false;
            }

            problem = null;
            return true;
        }

        // gzip은 1F 8B로 시작한다. Brotli에는 정해진 머리가 없어서 gzip이 아니면 Brotli로 본다.
        static bool IsGzip(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return stream.ReadByte() == 0x1F && stream.ReadByte() == 0x8B;
            }
        }

        // 보고서(과제)용: Build 파일별 크기와 폴더 전체 크기.
        public static string DescribeBuild(string output)
        {
            var lines = new System.Text.StringBuilder();
            var buildDir = Path.Combine(output, "Build");
            if (Directory.Exists(buildDir))
            {
                foreach (var file in Directory.GetFiles(buildDir).OrderByDescending(f => new FileInfo(f).Length))
                {
                    lines.AppendLine("  " + Path.GetFileName(file) + "  " + Mb(new FileInfo(file).Length));
                }
            }

            var all = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
            lines.Append("  total " + all.Length + " files, " + Mb(all.Sum(f => new FileInfo(f).Length)));
            return lines.ToString();
        }

        static string Mb(long bytes)
        {
            return (bytes / 1048576f).ToString("0.00") + "MB";
        }
    }
}
