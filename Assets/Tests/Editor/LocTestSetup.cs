using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.UI;

namespace ScrapFishing.Tests
{
    // 모든 테스트는 에디터 PlayerPrefs가 아닌 메모리 저장소(TestSaves)로 돈다. 개발자 PC의 저장 상태에 결과가 흔들리지 않는다.
    // 기존 테스트는 한국어 문구("음량", "해저 평원" 등)를 확인하므로 한국어로 두고,
    // 첫 플레이 튜토리얼은 끝난 것으로 둔다(튜토리얼이 뜨면 게임이 멈춰 시간·결과 흐름 테스트가 진행되지 않는다).
    [SetUpFixture]
    public class LocTestSetup
    {
        public const int TutorialDone = 2;

        [OneTimeSetUp]
        public void UseMemorySaves()
        {
            TestSaves.Begin();
            SaveStore.SetString(Loc.PrefKey, Loc.Code(GameLanguage.Ko));
            SaveStore.SetInt(FirstPlayGuide.Key, TutorialDone);
            Loc.Reload();
        }

        [OneTimeTearDown]
        public void Restore()
        {
            TestSaves.End();
            Loc.Reload();
        }
    }
}
