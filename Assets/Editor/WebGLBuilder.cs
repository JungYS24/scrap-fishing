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

            var output = Path.GetFullPath("docs");
            Directory.CreateDirectory(output);
            var buildDir = Path.Combine(output, "Build");
            if (Directory.Exists(buildDir))
            {
                Directory.Delete(buildDir, true);
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boat.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
            File.WriteAllText(Path.GetFullPath("webgl-build.status"), report.summary.result.ToString());
            var leftover = Path.Combine(output, "TemplateData");
            if (Directory.Exists(leftover))
            {
                Directory.Delete(leftover, true);
            }
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("WebGL build failed: " + report.summary.result);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            ValidateWebGlOutput(output);
            Debug.Log("WebGL build succeeded: " + output);
        }

        static void ValidateWebGlOutput(string output)
        {
            var indexPath = Path.Combine(output, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogError("WebGL index.html not found: " + indexPath);
                return;
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
                    continue;
                }

                var filePath = Path.Combine(buildDir, match.Groups[1].Value);
                if (!File.Exists(filePath))
                {
                    Debug.LogError("WebGL " + label + " file missing: " + filePath);
                }
            }

            var config = Regex.Match(html, @"var config\s*=\s*\{(?<body>[\s\S]*?)\};");
            if (!config.Success)
            {
                Debug.LogError("WebGL index.html config block was not found");
                return;
            }

            var lines = config.Groups["body"].Value.Split('\n');
            foreach (var line in lines)
            {
                if (line.TrimEnd().EndsWith("\";"))
                {
                    Debug.LogError("WebGL config line ends with a semicolon: " + line.Trim());
                }
            }
        }
    }
}
