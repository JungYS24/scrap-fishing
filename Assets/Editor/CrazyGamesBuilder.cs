using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScrapFishing.Build
{
    // CrazyGames 빌드. GitHub Pages(docs) 빌드와 같은 설정·템플릿을 쓰되, CRAZYGAMES 심볼을 켜고 Builds/CrazyGames에 따로 낸다.
    // 템플릿의 crazygames:remove 표시 구간(페이지 전체화면 버튼, 자동 전체화면, 인앱·iOS 안내, 세로 회전 안내)은 index.html에서 지운다.
    public static class CrazyGamesBuilder
    {
        public const string Define = "CRAZYGAMES";
        const string TargetMenuCrazyGames = "Tools/Scrap Fishing/Build Target: CrazyGames";
        const string TargetMenuGitHub = "Tools/Scrap Fishing/Build Target: GitHub Pages";

        static readonly Regex RemoveBlock = new Regex(
            @"[ \t]*(<!--|//) crazygames:remove-start.*?crazygames:remove-end( -->)?[ \t]*\r?\n",
            RegexOptions.Singleline);

        // 에디터에서 CrazyGames 동작을 미리 보려면 심볼을 켠다. 빌드 메뉴는 이 설정과 무관하게 심볼을 넣는다.
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
            var defines = CurrentDefines().Where(d => d != Define).ToList();
            if (on)
            {
                defines.Add(Define);
            }

            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, string.Join(";", defines));
            Debug.Log("Build target: " + (on ? "CrazyGames" : "GitHub Pages") + " (" + string.Join(";", defines) + ")");
        }

        static string[] CurrentDefines()
        {
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL)
                .Split(';')
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToArray();
        }

        [MenuItem("Tools/Scrap Fishing/Build CrazyGames", false, 30)]
        public static void Build()
        {
            WebGLBuilder.ApplySettings();
            var output = Path.GetFullPath(Path.Combine("Builds", "CrazyGames"));
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boat.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { Define }
            });

            var leftover = Path.Combine(output, "TemplateData");
            if (Directory.Exists(leftover))
            {
                Directory.Delete(leftover, true);
            }

            if (report.summary.result != BuildResult.Succeeded || !WebGLBuilder.ValidateWebGlOutput(output))
            {
                Debug.LogError("CrazyGames build failed: " + report.summary.result);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            var index = Path.Combine(output, "index.html");
            File.WriteAllText(index, StripForCrazyGames(File.ReadAllText(index)));
            Debug.Log("CrazyGames build succeeded: " + output);
        }

        public static string StripForCrazyGames(string html)
        {
            return RemoveBlock.Replace(html, string.Empty);
        }
    }
}
