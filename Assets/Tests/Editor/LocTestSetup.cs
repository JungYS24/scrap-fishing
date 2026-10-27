using NUnit.Framework;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Tests
{
    // 기존 테스트는 한국어 문구("음량", "해저 평원" 등)를 확인하므로 모든 테스트를 한국어로 돌린다.
    // 플레이 모드 진입(도메인 리로드) 뒤에도 유지되게 PlayerPrefs에 쓰고, 끝나면 원래 값으로 되돌린다.
    [SetUpFixture]
    public class LocTestSetup
    {
        string _saved;

        [OneTimeSetUp]
        public void PinKorean()
        {
            _saved = PlayerPrefs.HasKey(Loc.PrefKey) ? PlayerPrefs.GetString(Loc.PrefKey) : null;
            PlayerPrefs.SetString(Loc.PrefKey, Loc.Code(GameLanguage.Ko));
            Loc.Reload();
        }

        [OneTimeTearDown]
        public void Restore()
        {
            if (_saved != null)
            {
                PlayerPrefs.SetString(Loc.PrefKey, _saved);
            }
            else
            {
                PlayerPrefs.DeleteKey(Loc.PrefKey);
            }

            Loc.Reload();
        }
    }
}
