using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScrapFishing.Build
{
    // WebGL 용량 점검: docs는 건드리지 않고 Temp/sizecheck에 빌드한 뒤, 에셋별 압축 전 크기와 Build 파일 크기를 적는다.
    // 결과는 프로젝트 루트의 webgl-size-report.txt. 배포는 지금처럼 Build WebGL to docs로 한다.
    public static class BuildSizeReport
    {
        const string Output = "webgl-size-report.txt";

        [MenuItem("Scrap Fishing/WebGL Size Report")]
        public static void Run()
        {
            WebGLBuilder.ApplySettings();
            var staging = Path.GetFullPath(Path.Combine("Temp", "sizecheck"));
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boat.unity" },
                locationPathName = staging,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            var text = new StringBuilder();
            text.AppendLine("result " + report.summary.result + ", total " + Mb(report.summary.totalSize));
            var build = Path.Combine(staging, "Build");
            if (Directory.Exists(build))
            {
                foreach (var file in Directory.GetFiles(build))
                {
                    text.AppendLine(Path.GetFileName(file) + "  " + Mb((ulong)new FileInfo(file).Length));
                }
            }

            var byAsset = new Dictionary<string, ulong>();
            var byType = new Dictionary<string, ulong>();
            foreach (var packed in report.packedAssets)
            {
                foreach (var info in packed.contents)
                {
                    var path = string.IsNullOrEmpty(info.sourceAssetPath) ? "(built-in)" : info.sourceAssetPath;
                    byAsset[path] = (byAsset.TryGetValue(path, out var a) ? a : 0) + info.packedSize;
                    var type = info.type != null ? info.type.Name : "?";
                    byType[type] = (byType.TryGetValue(type, out var t) ? t : 0) + info.packedSize;
                }
            }

            text.AppendLine();
            text.AppendLine("== by type (uncompressed)");
            foreach (var pair in byType.OrderByDescending(p => p.Value))
            {
                text.AppendLine(Mb(pair.Value) + "  " + pair.Key);
            }

            text.AppendLine();
            text.AppendLine("== top assets (uncompressed)");
            foreach (var pair in byAsset.OrderByDescending(p => p.Value).Take(120))
            {
                text.AppendLine(Mb(pair.Value) + "  " + pair.Key);
            }

            File.WriteAllText(Path.GetFullPath(Output), text.ToString());
            Debug.Log("WebGL size report: " + Path.GetFullPath(Output));
        }

        static string Mb(ulong bytes)
        {
            return (bytes / 1048576f).ToString("0.00") + "MB";
        }
    }
}
