using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScrapFishing.Build
{
    public static class WebGLBuilder
    {
        [MenuItem("Scrap Fishing/Build WebGL to docs")]
        public static void Build()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            var output = Path.GetFullPath("docs");
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boat.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
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

