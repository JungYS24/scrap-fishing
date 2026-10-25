using NUnit.Framework;
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
        public void Reward_NeverOverflowsDollars()
        {
            Assert.AreEqual(5, SettingsView.CouponReward("smtm", int.MaxValue - 5));
            Assert.AreEqual(0, SettingsView.CouponReward("smtm", int.MaxValue));
        }
    }
}
