using System.IO;
using System.Reflection;
using NUnit.Framework;
using ScrapFishing.Build;
using ScrapFishing.Controls;
using ScrapFishing.Core;
using ScrapFishing.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    public class CrazyGamesTests
    {
        const string GitHubTemplate = "Assets/WebGLTemplates/ScrapFishing/index.html";
        const string CrazyGamesTemplate = "Assets/WebGLTemplates/ScrapFishingCrazyGames/index.html";

        [Test]
        public void CrazyGamesTemplate_DropsGitHubPagesExtras()
        {
            var github = File.ReadAllText(GitHubTemplate);
            var crazy = File.ReadAllText(CrazyGamesTemplate);
            string[] removed =
            {
                "manifest.webmanifest",
                "apple-touch-icon",
                "og:",
                "id=\"ios-install\"",
                "id=\"inapp-banner\"",
                "id=\"fs-button\"",
                "id=\"rotate-hint\"",
                "orientation.lock",
                "onUserActivate"
            };
            foreach (var part in removed)
            {
                StringAssert.Contains(part, github, "GitHub Pages 템플릿은 그대로");
                StringAssert.DoesNotContain(part, crazy, "CrazyGames 템플릿에서 빠짐: " + part);
            }

            StringAssert.DoesNotContain("serviceWorker", crazy);
            string[] kept =
            {
                "id=\"unity-progress-bar\"",
                "id=\"boot-error\"",
                "id=\"boot-reload\"",
                "Math.min(window.devicePixelRatio || 1, 2)",
                "#0b1220",
                "addEventListener(\"keydown\""
            };
            foreach (var part in kept)
            {
                StringAssert.Contains(part, crazy, "CrazyGames 템플릿에 남김: " + part);
            }

            Assert.IsTrue(File.Exists("Assets/WebGLTemplates/ScrapFishingCrazyGames/files.json"), "CrazyGames 업로드용 빌드 파일 목록");
        }

        [Test]
        public void BuildScope_RestoresPlayerSettings()
        {
            var template = PlayerSettings.WebGL.template;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            var compression = PlayerSettings.WebGL.compressionFormat;
            using (new BuildSettingsScope(CrazyGamesBuilder.Template, CrazyGamesBuilder.DefinesWith(true)))
            {
                Assert.AreEqual(CrazyGamesBuilder.Template, PlayerSettings.WebGL.template);
                StringAssert.Contains(CrazyGamesBuilder.Define, PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL));
            }

            Assert.AreEqual(template, PlayerSettings.WebGL.template);
            Assert.AreEqual(defines, PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL));
            Assert.AreEqual(compression, PlayerSettings.WebGL.compressionFormat);
            StringAssert.DoesNotContain(CrazyGamesBuilder.Define, CrazyGamesBuilder.DefinesWith(false));
        }

        [Test]
        public void Bridge_SendsOnlyChangesAndStopsDuringAds()
        {
            ResetBridge();
            var audioBefore = AudioListener.pause;
            try
            {
                CrazyGamesBridge.SetGameplay(false);
                Assert.IsNull(CrazyGamesBridge.LastSent, "시작한 적 없으면 멈춤도 보내지 않는다");

                CrazyGamesBridge.SetGameplay(true);
                Assert.AreEqual(true, CrazyGamesBridge.LastSent, "출항 → GameplayStart");

                CrazyGamesBridge.BeginAdBreak();
                Assert.AreEqual(false, CrazyGamesBridge.LastSent, "광고 중 → GameplayStop");
                Assert.IsTrue(AudioListener.pause, "광고 중 소리 끔");
                Assert.IsFalse(PointerUtil.TryRead(out _, out _, out _), "광고 중 입력 막음");

                CrazyGamesBridge.EndAdBreak();
                Assert.AreEqual(true, CrazyGamesBridge.LastSent, "광고 끝 → 다시 GameplayStart");
                Assert.AreEqual(audioBefore, AudioListener.pause);

                CrazyGamesBridge.SetGameplay(false);
                Assert.AreEqual(false, CrazyGamesBridge.LastSent, "결과·설정·오리파 → GameplayStop");
            }
            finally
            {
                CrazyGamesBridge.EndAdBreak();
                AudioListener.pause = audioBefore;
                ResetBridge();
            }
        }

        static void ResetBridge()
        {
            typeof(CrazyGamesBridge).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }

        [Test]
        public void BrowserLanguage_KoreanOnlyForKo()
        {
            Assert.AreEqual(GameLanguage.Ko, Loc.FromLanguageTag("ko"));
            Assert.AreEqual(GameLanguage.Ko, Loc.FromLanguageTag("ko-KR"));
            Assert.AreEqual(GameLanguage.Ko, Loc.FromLanguageTag(" KO-kr "));
            Assert.AreEqual(GameLanguage.En, Loc.FromLanguageTag("en-US"));
            Assert.AreEqual(GameLanguage.En, Loc.FromLanguageTag("ja"));
            Assert.AreEqual(GameLanguage.En, Loc.FromLanguageTag(""), "읽기 실패는 영어");
            Assert.AreEqual(GameLanguage.En, Loc.FromLanguageTag(null));
        }

        [Test]
        public void SmallWindow_LiftsKeyTextAndRestoresWhenLarger()
        {
            var canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
                area.SetParent(canvasGo.transform, false);
                var hud = new GameObject("Hud", typeof(RectTransform));
                hud.transform.SetParent(area, false);
                var key = UiFonts.CreateText(hud.transform, "Number", 14, TextAnchor.MiddleCenter);
                key.verticalOverflow = VerticalWrapMode.Truncate;
                var other = UiFonts.CreateText(area, "Notice", 14, TextAnchor.MiddleCenter);
                other.fontSize = 14;
                key.fontSize = 14;
                var lift = canvasGo.AddComponent<SmallWindowText>();
                lift.Bind(area);

                // 800×450 창: 게임이 약 0.5배 → 화면 10px이 되려면 20pt.
                area.localScale = new Vector3(0.5f, 0.5f, 1f);
                lift.Apply();
                Assert.AreEqual(20, key.fontSize, "핵심 글씨는 화면 10px 이상");
                Assert.AreEqual(VerticalWrapMode.Overflow, key.verticalOverflow, "커진 글씨가 잘려 사라지지 않게");
                Assert.AreEqual(14, other.fontSize, "핵심이 아닌 글씨는 그대로");

                // 창이 커지면 원래 크기로.
                area.localScale = Vector3.one;
                lift.Apply();
                Assert.AreEqual(14, key.fontSize);
                Assert.AreEqual(VerticalWrapMode.Truncate, key.verticalOverflow);

                // 코드가 바꾼 크기는 새 원래 크기가 된다.
                key.fontSize = 22;
                area.localScale = new Vector3(0.5f, 0.5f, 1f);
                lift.Apply();
                Assert.AreEqual(22, key.fontSize, "이미 충분히 크면 건드리지 않는다");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
