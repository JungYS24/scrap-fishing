using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.UI;

namespace ScrapFishing.Tests
{
    public class CouponTests
    {
        [Test]
        public void Smtm_Gives9999999Dollars()
        {
            Assert.AreEqual(9999999, SettingsView.CouponReward("smtm"));
            Assert.AreEqual(9999999, SettingsView.CouponReward("  SMTM "), "대소문자·앞뒤 공백 무시");
        }

        [Test]
        public void WrongCode_GivesNothing()
        {
            Assert.AreEqual(0, SettingsView.CouponReward(""));
            Assert.AreEqual(0, SettingsView.CouponReward(null));
            Assert.AreEqual(0, SettingsView.CouponReward("smt"));
            Assert.AreEqual(0, SettingsView.CouponReward("s m t m"));
        }

        [Test]
        public void Reset_IsRecognizedAndGivesNoDollars()
        {
            Assert.IsTrue(SaveReset.IsResetCode("reset"));
            Assert.IsTrue(SaveReset.IsResetCode("  RESET "), "대소문자·앞뒤 공백 무시");
            Assert.IsFalse(SaveReset.IsResetCode("rese"));
            Assert.IsFalse(SaveReset.IsResetCode(null));
            Assert.IsFalse(SaveReset.IsResetCode("smtm"));
            Assert.AreEqual(0, SettingsView.CouponReward("reset"), "reset은 달러 쿠폰이 아니다");
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
        public void Reward_NeverOverflowsDollars()
        {
            Assert.AreEqual(5, SettingsView.CouponReward("smtm", int.MaxValue - 5));
            Assert.AreEqual(0, SettingsView.CouponReward("smtm", int.MaxValue));
        }
    }
}
