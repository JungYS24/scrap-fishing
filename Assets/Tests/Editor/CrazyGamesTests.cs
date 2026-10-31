using System.IO;
using NUnit.Framework;
using ScrapFishing.Build;
using ScrapFishing.Core;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    public class CrazyGamesTests
    {
        const string TemplatePath = "Assets/WebGLTemplates/ScrapFishing/index.html";

        [Test]
        public void Template_StripsFullscreenAndRotateHintOnlyForCrazyGames()
        {
            var html = File.ReadAllText(TemplatePath);
            var stripped = CrazyGamesBuilder.StripForCrazyGames(html);
            string[] removed =
            {
                "id=\"fs-button\"",
                "id=\"rotate-hint\"",
                "id=\"inapp-banner\"",
                "id=\"ios-install\"",
                "addEventListener(\"pointerup\", onUserActivate)",
                "addEventListener(\"touchend\", onUserActivate"
            };
            foreach (var part in removed)
            {
                StringAssert.Contains(part, html, "GitHub Pages 템플릿은 그대로");
                StringAssert.DoesNotContain(part, stripped, "CrazyGames에서 빠짐: " + part);
            }

            StringAssert.DoesNotContain("crazygames:remove", stripped, "표시 줄도 남지 않는다");
            StringAssert.Contains("createUnityInstance", stripped);
            StringAssert.Contains("addEventListener(\"keydown\"", stripped, "스페이스·방향키 스크롤 막기는 남는다");
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
