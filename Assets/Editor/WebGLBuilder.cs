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

            Debug.Log("WebGL build succeeded: " + output);
        }
    }
}
