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
        const int Seed = 20261024;

        static readonly string[] MachineIds = { "machine_lure", "machine_rod", "machine_boat", "machine_drone", "machine_fisher", "machine_helmet" };
        static readonly int[] PerGrade = { 5, 4, 3, 2, 1 };
        static readonly GearStat[] LegendOnly = { GearStat.SpawnCapBonus, GearStat.DiveEveryCastsBonus, GearStat.BonusCatchChancePercent };

        GachaMachineData[] _machines;
        GachaCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _machines = OripaCatalogJson.Load();
            Assert.IsNotNull(_machines, "Resources/Gacha/oripa_rewards.json");
            _catalog = new GachaCatalog(_machines);
            GearBonuses.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GearBonuses.Clear();
            foreach (var machine in _machines)
            {
                foreach (var gear in machine.Gears)
                {
                    Object.DestroyImmediate(gear);
                }

                Object.DestroyImmediate(machine);
            }
        }

        GachaMachineData Machine(GearSlot slot)
        {
            return _catalog.MachineForSlot(slot);
        }

        [Test]
        public void Json_SixMachinesNinetyItemsFiveFourThreeTwoOne()
        {
            CollectionAssert.AreEqual(MachineIds, _machines.Select(m => m.Id).ToArray());
            Assert.AreEqual(90, _machines.Sum(m => m.Gears.Count));
            foreach (var machine in _machines)
            {
                for (var r = 0; r < PerGrade.Length; r++)
                {
                    Assert.AreEqual(PerGrade[r], machine.Gears.Count(g => g.Rarity == (ItemRarity)r), machine.Id + " " + (ItemRarity)r);
                }

                Assert.IsTrue(machine.Gears.All(g => g.Slot == machine.Slot), machine.Id);
            }
        }

        [Test]
        public void Json_PricesAreBasePriceTimesPricePerPull()
        {
            Assert.AreEqual(5000, Machine(GearSlot.Lure).Price);
            Assert.AreEqual(5000, Machine(GearSlot.Rod).Price);
            Assert.AreEqual(7500, Machine(GearSlot.Boat).Price);
            Assert.AreEqual(7500, Machine(GearSlot.Drone).Price);
            Assert.AreEqual(7500, Machine(GearSlot.Fisher).Price);
            Assert.AreEqual(5000, Machine(GearSlot.Helmet).Price);
        }

        [Test]
        public void Json_GradeRatesSumToOneAndRefundsFollowGrades()
        {
            foreach (var machine in _machines)
            {
                var sum = 0f;
                for (var r = 0; r < GachaService.RarityCount; r++)
                {
                    sum += machine.BaseRate((ItemRarity)r);
                }

                Assert.AreEqual(100f, sum, 1e-3f, machine.Id);
                Assert.AreEqual(101, machine.BoostCycle, machine.Id);
            }

            Assert.AreEqual(0.3f, _catalog.RefundMultiplier(ItemRarity.Normal), 1e-4f);
            Assert.AreEqual(8f, _catalog.RefundMultiplier(ItemRarity.Legend), 1e-4f);
            Assert.AreEqual(25f, _catalog.RefundMultiplier(ItemRarity.Synchro), 1e-4f);
        }

        [Test]
        public void Pulls_TenThousandMatchRatesWithinOnePoint()
        {
            var service = new GachaService(new SystemGachaRandom(Seed));
            foreach (var machine in _machines)
            {
                var counts = new int[GachaService.RarityCount];
                var counter = 0;
                for (var i = 0; i < 10000; i++)
                {
                    counts[(int)service.Pull(machine, 1, ref counter, false)[0].Rarity]++;
                }

                for (var r = 0; r < counts.Length; r++)
                {
                    var expected = machine.BaseRate((ItemRarity)r) / 100f;
                    Assert.AreEqual(expected, counts[r] / 10000f, 0.01f, machine.Id + " " + (ItemRarity)r);
                }
            }
        }

        [Test]
        public void LegendOnlyEffects_AreAbsentBelowLegend()
        {
            foreach (var gear in _machines.SelectMany(m => m.Gears))
            {
                if (gear.Rarity >= ItemRarity.Legend)
                {
                    continue;
                }

                foreach (var stat in LegendOnly)
                {
                    Assert.AreEqual(0f, gear.Value(stat), gear.Id + " " + stat);
                }
            }

            Assert.Greater(_catalog.FindGear("rod_legend_01").Value(GearStat.BonusCatchChancePercent), 0f);
            Assert.Greater(_catalog.FindGear("fisher_legend_01").Value(GearStat.DiveEveryCastsBonus), 0f);
            Assert.Greater(_catalog.FindGear("lure_legend_01").Value(GearStat.SpawnCapBonus), 0f);
        }

        [Test]
        public void Json_MapsEffectKeysBySlot()
        {
            var drone = _catalog.FindGear("drone_legend_01");
            Assert.AreEqual(28f, drone.Value(GearStat.DroneRadiusPercent), "드론의 magnetRadiusPct는 잠수 흡착 반경");
            Assert.AreEqual(0f, drone.Value(GearStat.CatchRadiusPercent));

            var rod = _catalog.FindGear("rod_synchro_01");
            Assert.AreEqual(40f, rod.Value(GearStat.HookHitRadiusPercent));
            Assert.AreEqual(25f, rod.Value(GearStat.MaxDepthPercent));
            Assert.AreEqual(20f, rod.Value(GearStat.BonusCatchChancePercent));

            var helmet = _catalog.FindGear("helmet_synchro_01");
            Assert.AreEqual(GearSlot.Helmet, helmet.Slot);
            Assert.AreEqual(100f, helmet.Value(GearStat.HazardDamageReductionPercent));
        }

        [Test]
        public void DiveCadence_NeverBelowOneCast()
        {
            Assert.AreEqual(3, RunSession.CastsPerDiveFor(0));
            Assert.AreEqual(2, RunSession.CastsPerDiveFor(1));
            Assert.AreEqual(1, RunSession.CastsPerDiveFor(2));
            Assert.AreEqual(1, RunSession.CastsPerDiveFor(10));
        }

        [Test]
        public void Bonuses_SumEquippedGearAcrossSixSlots()
        {
            var inventory = new GearInventory(_catalog);
            var ids = new[] { "lure_legend_01", "rod_legend_01", "fisher_legend_01", "helmet_synchro_01" };
            inventory.AddPulled(ids.Select(_catalog.FindGear).ToList(), "test", 0L);
            foreach (var id in ids)
            {
                Assert.IsTrue(inventory.Equip(id), id);
            }

            GearBonuses.Refresh(inventory);
            Assert.AreEqual(3f, GearBonuses.Get(GearStat.LureCapacity));
            Assert.AreEqual(1.25f, GearBonuses.Scale(GearStat.HookHitRadiusPercent), 1e-5f);
            Assert.AreEqual(1.2f, GearBonuses.Scale(GearStat.SteerSpeedPercent), 1e-5f);
            Assert.AreEqual(ItemRarity.Legend, GearBonuses.Equipped(GearSlot.Fisher).Rarity);
            Assert.AreEqual(0.49f + 0.51f * 0.15f, GearBonuses.MaxDepth(0.49f), 1e-5f, "남은 수심의 15%");
            Assert.AreEqual(1f, GearBonuses.MaxDepth(1f), 1e-5f, "바닥 아래로는 내려가지 않는다");

            var withoutGear = LureLoad.CapacityForLevel(Progression.Level(UpgradeKind.Reel), new[] { 4, 5, 6, 7, 8, 9 });
            Assert.AreEqual(withoutGear + 3, RodStats.MaxAttach);
            Assert.AreEqual(14, DepthZone.SpawnCount(1f, 20, 2));
        }

        [Test]
        public void Migration_OldSaveWithSuitKeepsDollarsAndEquipment()
        {
            var store = new MemoryKeyValueStore();
            store.SetInt(SaveMigration.VersionKey, 3);
            store.SetInt(Progression.DollarKey, 1000);
            store.SetInt(Progression.LevelKey(UpgradeKind.Reel), 4);
            var old = new GachaSaveData();
            old.gear.Add(new GearStack { id = "lure_rare_01", unprocessed = 1 });
            old.gear.Add(new GearStack { id = "suit_normal_01", stored = 2 });
            old.gear.Add(new GearStack { id = "suit_raincoat", unprocessed = 1 });
            old.equipped = new string[5];
            old.equipped[(int)GearSlot.Lure] = "lure_legend_01";
            old.equipped[(int)GearSlot.Helmet] = "suit_legend_01";
            old.counters.Add(new GachaCounter { machine = "machine_suit", count = 40 });
            old.counters.Add(new GachaCounter { machine = "machine_lure", count = 7 });
            store.SetString(GearInventory.SaveKey, JsonUtility.ToJson(old));

            Assert.IsTrue(SaveMigration.Run(store, _catalog));

            var normal = Mathf.RoundToInt(5000 * 0.3f);
            var legend = Mathf.RoundToInt(5000 * 8f);
            Assert.AreEqual(1000 + normal * 2 + normal + legend, store.GetInt(Progression.DollarKey, 0), "옛 수트 장비 4개를 등급 환원가로 돌려준다");
            Assert.AreEqual(4, store.GetInt(Progression.LevelKey(UpgradeKind.Reel), 0));
            Assert.AreEqual(SaveMigration.CurrentVersion, store.GetInt(SaveMigration.VersionKey, 0));

            var inventory = GearInventory.Load(store, _catalog);
            Assert.AreEqual("lure_legend_01", inventory.EquippedId(GearSlot.Lure), "새 카탈로그에 있는 장착품은 그대로");
            Assert.IsNull(inventory.EquippedId(GearSlot.Helmet));
            Assert.IsNull(inventory.EquippedId(GearSlot.Rod));
            Assert.AreEqual(1, inventory.Count("lure_rare_01", GearState.Unprocessed));
            Assert.AreEqual(40, inventory.Counter("machine_helmet"), "수트 머신 카운트는 헬멧 머신으로");
            Assert.AreEqual(7, inventory.Counter("machine_lure"));

            Assert.IsFalse(SaveMigration.Run(store, _catalog), "다시 돌려도 바뀌지 않는다");
        }
    }
}
