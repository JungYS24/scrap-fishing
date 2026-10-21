using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScrapFishing.Build
{
    public static class WebGLBuilder
    {
        [MenuItem("Scrap Fishing/Build WebGL to docs")]
        public static void Build()
        {
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:ScrapFishing";

            // Unity names Build files after the last folder, so this must stay "docs".
            var staging = Path.GetFullPath(Path.Combine("Temp", "docs"));
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }

            Directory.CreateDirectory(staging);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boat.unity" },
                locationPathName = staging,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText(Path.GetFullPath("webgl-build.status"), report.summary.result.ToString());
            var leftover = Path.Combine(staging, "TemplateData");
            if (Directory.Exists(leftover))
            {
                Directory.Delete(leftover, true);
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("WebGL build failed: " + report.summary.result + ". docs was left unchanged.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            if (!ValidateWebGlOutput(staging))
            {
                Debug.LogError("WebGL output check failed. docs was left unchanged.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            CopyShareFiles(staging);
            var output = Path.GetFullPath("docs");
            PublishStaging(staging, output);
            Debug.Log("WebGL build succeeded: " + output);
        }

        static readonly string[] ShareFiles =
        {
            "og-image.png",
            "apple-touch-icon.png",
            "icon-192.png",
            "icon-512.png"
        };

        static void CopyShareFiles(string staging)
        {
            var template = Path.GetFullPath(Path.Combine("Assets", "WebGLTemplates", "ScrapFishing"));
            foreach (var name in ShareFiles)
            {
                var source = Path.Combine(template, name);
                if (!File.Exists(source))
                {
                    Debug.LogWarning("공유 이미지가 템플릿에 없습니다: " + source);
                    continue;
                }

                File.Copy(source, Path.Combine(staging, name), true);
            }
        }

        static void PublishStaging(string staging, string output)
        {
            Directory.CreateDirectory(output);
            var stagingBuild = Path.Combine(staging, "Build");
            var publishedBuild = Path.Combine(output, "Build");
            var incomingBuild = Path.Combine(output, "Build.next");
            if (Directory.Exists(incomingBuild))
            {
                Directory.Delete(incomingBuild, true);
            }

            CopyDirectory(stagingBuild, incomingBuild);
            if (Directory.Exists(publishedBuild))
            {
                Directory.Delete(publishedBuild, true);
            }

            Directory.Move(incomingBuild, publishedBuild);

            foreach (var file in Directory.GetFiles(staging))
            {
                var destination = Path.Combine(output, Path.GetFileName(file));
                File.Copy(file, destination, true);
            }

            foreach (var dir in Directory.GetDirectories(staging))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals("Build", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destination = Path.Combine(output, name);
                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, true);
                }

                CopyDirectory(dir, destination);
            }

            var publishedTemplateData = Path.Combine(output, "TemplateData");
            if (Directory.Exists(publishedTemplateData))
            {
                Directory.Delete(publishedTemplateData, true);
            }

            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
        }

        static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
            }
        }

        static bool ValidateWebGlOutput(string output)
        {
            var ok = true;
            var indexPath = Path.Combine(output, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogError("WebGL index.html not found: " + indexPath);
                return false;
            }

            var html = File.ReadAllText(indexPath);
            var buildDir = Path.Combine(output, "Build");
            var checks = new[]
            {
                new[] { "loaderUrl", "loader" },
                new[] { "dataUrl", "data" },
                new[] { "frameworkUrl", "framework" },
                new[] { "codeUrl", "wasm" }
            };

            foreach (var check in checks)
            {
                var key = check[0];
                var label = check[1];
                var match = Regex.Match(html, key + @"\s*[:=]\s*buildUrl\s*\+\s*""/([^""]+)""");
                if (!match.Success)
                {
                    Debug.LogError("WebGL index.html is missing a " + label + " path (" + key + ")");
                    ok = false;
                    continue;
                }

                var filePath = Path.Combine(buildDir, match.Groups[1].Value);
                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    Debug.LogError("WebGL " + label + " file missing or empty: " + filePath);
                    ok = false;
                }
            }

            var config = Regex.Match(html, @"var config\s*=\s*\{(?<body>[\s\S]*?)\};");
            if (!config.Success)
            {
                Debug.LogError("WebGL index.html config block was not found");
                return false;
            }

            var lines = config.Groups["body"].Value.Split('\n');
            foreach (var line in lines)
            {
                if (line.TrimEnd().EndsWith("\";"))
                {
                    Debug.LogError("WebGL config line ends with a semicolon: " + line.Trim());
                    ok = false;
                }
            }

            return ok;
        }
    }
}
