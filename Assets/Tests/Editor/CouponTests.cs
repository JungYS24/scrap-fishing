using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.UI;

namespace ScrapFishing.Tests
{
    public class CouponTests
    {
        // 에디터를 CrazyGames 대상으로 바꿔 둬도 같은 결과가 나오게, 쿠폰이 켜진 경우를 직접 지정해 계산한다.
        static int Reward(string code, int currentDollars = 0, bool eventUsed = false)
        {
            return SettingsView.CouponReward(code, currentDollars, eventUsed, true);
        }

        [Test]
        public void Smtm_Gives9999999Dollars()
        {
            Assert.AreEqual(9999999, Reward("smtm"));
            Assert.AreEqual(9999999, Reward("  SMTM "), "대소문자·앞뒤 공백 무시");
        }

        [Test]
        public void Fish_Gives500000Once()
        {
            Assert.AreEqual(500000, Reward("fish"));
            Assert.AreEqual(500000, Reward(" FISH "), "대소문자·앞뒤 공백 무시");
            Assert.AreEqual(0, Reward("fish", 0, true), "이미 받았으면 0");
            Assert.AreEqual(9999999, Reward("smtm", 0, true), "smtm은 fish 사용 여부와 무관");
        }

        [Test]
        public void WrongCode_GivesNothing()
        {
            Assert.AreEqual(0, Reward(""));
            Assert.AreEqual(0, Reward(null));
            Assert.AreEqual(0, Reward("smt"));
            Assert.AreEqual(0, Reward("s m t m"));
        }

        [Test]
        public void Reset_IsRecognizedAndGivesNoDollars()
        {
            Assert.IsTrue(SaveReset.IsResetCode("reset"));
            Assert.IsTrue(SaveReset.IsResetCode("  RESET "), "대소문자·앞뒤 공백 무시");
            Assert.IsFalse(SaveReset.IsResetCode("rese"));
            Assert.IsFalse(SaveReset.IsResetCode(null));
            Assert.IsFalse(SaveReset.IsResetCode("smtm"));
            Assert.AreEqual(0, Reward("reset"), "reset은 달러 쿠폰이 아니다");
        }

        // 실제 저장(PlayerPrefs)은 지우지 않도록 지우기 동작을 바꿔 끼워 순서만 확인한다.
        [Test]
        public void Reset_WipesThenReloadsSettings()
        {
            var wiped = 0;
            SaveReset.ResetAll(() => wiped++, false);
            Assert.AreEqual(1, wiped);
        }

        [Test]
        public void CrazyGames_CheatCouponsAreInvalid()
        {
            Assert.AreEqual(0, SettingsView.CouponReward("smtm", 0, false, false));
            Assert.AreEqual(0, SettingsView.CouponReward("fish", 0, false, false));
            Assert.AreEqual(0, SettingsView.CouponReward("reset", 0, false, false));
#if CRAZYGAMES
            Assert.IsFalse(SettingsView.CheatCouponsEnabled);
            Assert.AreEqual(0, SettingsView.CouponReward("smtm"));
#else
            Assert.IsTrue(SettingsView.CheatCouponsEnabled);
            Assert.AreEqual(9999999, SettingsView.CouponReward("smtm"));
#endif
        }

        [Test]
        public void Reward_NeverOverflowsDollars()
        {
            Assert.AreEqual(5, Reward("smtm", int.MaxValue - 5));
            Assert.AreEqual(0, Reward("smtm", int.MaxValue));
        }
    }
}
