using NUnit.Framework;
using ScrapFishing.Fishing;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class LureLoadTests
    {
        [Test]
        public void Attach_SucceedsUntilCapacity()
        {
            var load = new LureLoad(4);
            Assert.IsTrue(load.TryAttach(1, 80));
            Assert.IsTrue(load.TryAttach(2, 80));
            Assert.IsTrue(load.TryAttach(3, 80));
            Assert.IsTrue(load.TryAttach(4, 80));
            Assert.IsFalse(load.TryAttach(5, 80));
            Assert.AreEqual(4, load.Count);
            Assert.IsTrue(load.IsFull);
        }

        [Test]
        public void Hazard_DoesNotAttach()
        {
            var load = new LureLoad(4);
            Assert.IsTrue(LureLoad.IsHazard(-25));
            Assert.IsFalse(load.TryAttach(9, -25));
            Assert.AreEqual(0, load.Count);
        }

        [Test]
        public void Cash_IsIdempotent()
        {
            var load = new LureLoad(4);
            Assert.IsTrue(load.TryAttach(7, 200));
            Assert.IsTrue(load.TryCash(7));
            Assert.IsFalse(load.TryCash(7));
        }

        [Test]
        public void Capacity_UsesLastValueWhenLevelOverflows()
        {
            var table = new[] { 4, 6, 8, 10, 12 };
            Assert.AreEqual(4, LureLoad.CapacityForLevel(0, table));
            Assert.AreEqual(6, LureLoad.CapacityForLevel(1, table));
            Assert.AreEqual(12, LureLoad.CapacityForLevel(4, table));
            Assert.AreEqual(12, LureLoad.CapacityForLevel(99, table));
        }

        [Test]
        public void LegacyScrap_ConvertsChip200ToGold10()
        {
            Assert.AreEqual(10, LureLoad.GoldFromAmount(200));
        }

        [Test]
        public void RodStats_OverflowUsesLastCapacity()
        {
            var previous = PlayerPrefs.GetInt(RodStats.LevelKey, 0);
            try
            {
                PlayerPrefs.SetInt(RodStats.LevelKey, 99);
                PlayerPrefs.Save();
                Assert.AreEqual(12, RodStats.MaxAttach);
            }
            finally
            {
                PlayerPrefs.SetInt(RodStats.LevelKey, previous);
                PlayerPrefs.Save();
            }
        }
    }
}
