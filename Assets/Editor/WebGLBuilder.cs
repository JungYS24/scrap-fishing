using System.IO;
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
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("WebGL build failed: " + report.summary.result);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            DisableBrowserGestures(output);
            Debug.Log("WebGL build succeeded: " + output);
        }

        const string TouchCss = "html, body { overscroll-behavior: none; touch-action: none; -webkit-user-select: none; user-select: none }\n#unity-canvas { touch-action: none; -webkit-touch-callout: none; -webkit-tap-highlight-color: transparent }\n";

        static void DisableBrowserGestures(string output)
        {
            var css = Path.Combine(output, "TemplateData", "style.css");
            if (!File.Exists(css))
            {
                return;
            }

            var text = File.ReadAllText(css);
            if (!text.Contains("touch-action"))
            {
                File.WriteAllText(css, text.TrimEnd('\n', '\r') + "\n" + TouchCss);
            }
        }
    }
}
