using System.Linq;
using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.Fishing;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class OripaCatalogTests
    {
        GachaMachineData[] _machines;

        [SetUp]
        public void SetUp()
        {
            _machines = OripaCatalogJson.Load();
            GearBonuses.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GearBonuses.Clear();
            if (_machines == null)
            {
                return;
            }

            foreach (var machine in _machines)
            {
                foreach (var gear in machine.Gears)
                {
                    Object.DestroyImmediate(gear);
                }

                Object.DestroyImmediate(machine);
            }
        }

        [Test]
        public void Json_LoadsFiveMachinesWithFifteenGearEach()
        {
            Assert.IsNotNull(_machines, "Resources/Gacha/oripa_rewards.json");
            CollectionAssert.AreEqual(
                new[] { "machine_lure", "machine_boat", "machine_drone", "machine_fisher", "machine_suit" },
                _machines.Select(m => m.Id).ToArray());
            foreach (var machine in _machines)
            {
                Assert.AreEqual(15, machine.Gears.Count, machine.Id);
                Assert.IsTrue(machine.Gears.All(g => g.Slot == machine.Slot), machine.Id);
                Assert.AreEqual(1, machine.Gears.Count(g => g.Rarity == ItemRarity.Synchro), machine.Id);
            }

            Assert.AreEqual(GearSlot.Fisher, _machines[3].Slot);
        }

        [Test]
        public void Json_PricesUseFiveThousandBaseWhenUnset()
        {
            Assert.AreEqual(5000, _machines[0].Price, "루어 1.0P");
            Assert.AreEqual(7500, _machines[1].Price, "보트 1.5P");
            Assert.AreEqual(7500, _machines[3].Price, "피셔 1.5P");
            Assert.AreEqual(5000, _machines[4].Price, "수트 1.0P");
        }

        [Test]
        public void Json_RatesAndRefundsFollowGrades()
        {
            var lure = _machines[0];
            Assert.AreEqual(70f, lure.BaseRate(ItemRarity.Normal), 1e-3f);
            Assert.AreEqual(21f, lure.BaseRate(ItemRarity.Rare), 1e-3f);
            Assert.AreEqual(7f, lure.BaseRate(ItemRarity.Epic), 1e-3f);
            Assert.AreEqual(1.6f, lure.BaseRate(ItemRarity.Legend), 1e-3f);
            Assert.AreEqual(0.4f, lure.BaseRate(ItemRarity.Synchro), 1e-3f);
            Assert.AreEqual(101, lure.BoostCycle, "pityLegendAt 100 → 101회 주기");

            var synchro = lure.Gears.First(g => g.Rarity == ItemRarity.Synchro);
            Assert.AreEqual(25f, synchro.RefundMultiplier, 1e-4f);
            Assert.AreEqual("아마테라스 싱크로 코어", synchro.DisplayName);
            Assert.IsFalse(string.IsNullOrEmpty(synchro.Description));
        }

        [Test]
        public void Json_MapsEffectKeysBySlot()
        {
            var drone = _machines[2].Gears.First(g => g.Id == "drone_legend_01");
            Assert.AreEqual(28f, drone.Value(GearStat.DroneRadiusPercent), "드론의 magnetRadiusPct는 잠수 흡착 반경");
            Assert.AreEqual(0f, drone.Value(GearStat.CatchRadiusPercent));
            Assert.AreEqual(1f, drone.Value(GearStat.HazardShield));

            var lure = _machines[0].Gears.First(g => g.Id == "lure_legend_01");
            Assert.AreEqual(22f, lure.Value(GearStat.CatchRadiusPercent));
            Assert.AreEqual(2f, lure.Value(GearStat.SpawnCapBonus));

            var fisher = _machines[3].Gears.First(g => g.Id == "fisher_synchro_01");
            Assert.AreEqual(30f, fisher.Value(GearStat.SteerSpeedPercent));
            Assert.AreEqual(1f, fisher.Value(GearStat.DiveEveryCastsBonus));

            var zero = _machines[0].Gears.First(g => g.Id == "lure_normal_01");
            Assert.AreEqual(1, zero.Effects.Count, "0인 효과는 넣지 않는다");
        }

        [Test]
        public void Bonuses_SumEquippedGearAcrossSlots()
        {
            var catalog = new GachaCatalog(_machines);
            var inventory = new GearInventory(catalog);
            inventory.AddPulled(new[] { catalog.FindGear("lure_legend_01"), catalog.FindGear("fisher_legend_01"), catalog.FindGear("suit_synchro_01") }, "test", 0L);
            Assert.IsTrue(inventory.Equip("lure_legend_01"));
            Assert.IsTrue(inventory.Equip("fisher_legend_01"));
            Assert.IsTrue(inventory.Equip("suit_synchro_01"));

            GearBonuses.Refresh(inventory);
            Assert.AreEqual(3f, GearBonuses.Get(GearStat.LureCapacity));
            Assert.AreEqual(1.2f, GearBonuses.Scale(GearStat.SteerSpeedPercent), 1e-5f);
            Assert.AreEqual(100f, GearBonuses.Get(GearStat.HazardDamageReductionPercent));
            Assert.AreEqual(1, GearBonuses.Count(GearStat.DiveEveryCastsBonus));

            Assert.AreEqual(RodStats.MaxAttach, RodStatsWithoutGear() + 3, "루어 용량이 MaxAttach에 더해진다");
            Assert.AreEqual(14, DepthZone.SpawnCount(1f, 20, 2), "스폰 상한 12 + 2");
            Assert.AreEqual(12, DepthZone.SpawnCount(1f, 20), "가산이 없으면 12");
        }

        [Test]
        public void Bonuses_ScaleNeverDropsBelowFloor()
        {
            var catalog = new GachaCatalog(_machines);
            var inventory = new GearInventory(catalog);
            inventory.AddPulled(new[] { catalog.FindGear("boat_synchro_01") }, "test", 0L);
            inventory.Equip("boat_synchro_01");
            GearBonuses.Refresh(inventory);
            Assert.AreEqual(0.8f, GearBonuses.Scale(GearStat.GaugeSpeedPercent), 1e-5f, "gaugeSpeedPct -20 → 0.8배");
            Assert.AreEqual(15f, GearBonuses.Get(GearStat.EndBonusPercent));
        }

        static int RodStatsWithoutGear()
        {
            return LureLoad.CapacityForLevel(Progression.Level(UpgradeKind.Reel), new[] { 4, 5, 6, 7, 8, 9 });
        }
    }
}
