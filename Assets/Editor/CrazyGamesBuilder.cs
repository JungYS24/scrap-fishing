using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScrapFishing.Build
{
    // CrazyGames 빌드. GitHub Pages(docs) 빌드와 같은 압축·최적화 설정을 쓰되, CrazyGames 전용 템플릿
    // (WebGLTemplates/ScrapFishingCrazyGames)과 CRAZYGAMES 심볼로 Builds/CrazyGames에 따로 낸다.
    // 빌드가 끝나면 템플릿·심볼 등 PlayerSettings를 원래 값으로 되돌린다(BuildSettingsScope).
    public static class CrazyGamesBuilder
    {
        public const string Define = "CRAZYGAMES";
        public const string Template = "PROJECT:ScrapFishingCrazyGames";
        public const string BootScene = "Assets/Scenes/CrazyGamesBoot.unity";
        const string TargetMenuCrazyGames = "Tools/Scrap Fishing/Build Target: CrazyGames";
        const string TargetMenuGitHub = "Tools/Scrap Fishing/Build Target: GitHub Pages";

        // 에디터에서 CrazyGames 동작을 미리 보려면 심볼을 켠다. 빌드 메뉴는 이 설정과 무관하게 대상별 심볼로 빌드한다.
        public static bool IsCrazyGamesTarget => CurrentDefines().Contains(Define);

        [MenuItem(TargetMenuCrazyGames, false, 10)]
        static void UseCrazyGames()
        {
            SetCrazyGames(true);
        }

        [MenuItem(TargetMenuCrazyGames, true)]
        static bool UseCrazyGamesCheck()
        {
            Menu.SetChecked(TargetMenuCrazyGames, IsCrazyGamesTarget);
            return true;
        }

        [MenuItem(TargetMenuGitHub, false, 11)]
        static void UseGitHubPages()
        {
            SetCrazyGames(false);
        }

        [MenuItem(TargetMenuGitHub, true)]
        static bool UseGitHubPagesCheck()
        {
            Menu.SetChecked(TargetMenuGitHub, !IsCrazyGamesTarget);
            return true;
        }

        public static void SetCrazyGames(bool on)
        {
            var defines = DefinesWith(on);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, defines);
            Debug.Log("Build target: " + (on ? "CrazyGames" : "GitHub Pages") + " (" + defines + ")");
        }

        // 지금 WebGL 심볼에서 CRAZYGAMES만 켜거나 끈 문자열.
        public static string DefinesWith(bool crazyGames)
        {
            var defines = CurrentDefines().Where(d => d != Define).ToList();
            if (crazyGames)
            {
                defines.Add(Define);
            }

            return string.Join(";", defines);
        }

        static string[] CurrentDefines()
        {
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL)
                .Split(';')
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToArray();
        }

        [MenuItem("Build/CrazyGames WebGL", false, 1)]
        public static void Build()
        {
            var output = Path.GetFullPath(Path.Combine("Builds", "CrazyGames"));
            bool ok;
            using (new BuildSettingsScope(Template, DefinesWith(true)))
            {
                ok = BuildTo(output);
            }

            if (!ok)
            {
                Debug.LogError("CrazyGames build failed.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log("CrazyGames build succeeded: " + output);
        }

        static bool BuildTo(string output)
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                // 첫 씬에서 SDK 초기화를 기다린 뒤 게임 씬을 연다(저장은 초기화 뒤에만 읽는다).
                scenes = new[] { BootScene, "Assets/Scenes/Boat.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            File.WriteAllText(Path.GetFullPath("webgl-build.status"), report.summary.result.ToString());

            var leftover = Path.Combine(output, "TemplateData");
            if (Directory.Exists(leftover))
            {
                Directory.Delete(leftover, true);
            }

            return report.summary.result == BuildResult.Succeeded && WebGLBuilder.ValidateWebGlOutput(output);
        }
    }
}
