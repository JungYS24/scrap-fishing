using System.Collections.Generic;
using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class GachaServiceTests
    {
        const int Seed = 20261022;

        GachaMachineData[] _machines;
        GachaCatalog _catalog;
        GachaMachineData _lure;
        GachaMachineData _boat;

        sealed class FixedRandom : IGachaRandom
        {
            readonly float _value;

            public FixedRandom(float value)
            {
                _value = value;
            }

            public float Next()
            {
                return _value;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _machines = GachaDefaults.CreateMachines();
            _catalog = new GachaCatalog(_machines);
            _lure = _catalog.FindMachine("machine_lure");
            _boat = _catalog.FindMachine("machine_boat");
        }

        [TearDown]
        public void TearDown()
        {
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
        public void Distribution_BaseRates_Within1pp()
        {
            var shares = SinglePullShares(_lure, 10000, false);
            AssertShares(shares, 0.70f, 0.22f, 0.07f, 0.01f, 0.01f);
        }

        [Test]
        public void Distribution_WithBoost_Within1pp()
        {
            var shares = SinglePullShares(_lure, 10000, true);
            AssertShares(shares, 0.70f, 0.22f, 0.07f, 0.01f, 0.01f);
        }

        [Test]
        public void Boost_LongRunMatchesEffectiveRates()
        {
            var effective = GachaService.EffectiveRates(_lure);
            var shares = SinglePullShares(_lure, 200000, true);
            Debug.Log("[Gacha] 보간 포함 장기 확률 N/R/E/L = "
                + Percent(effective[0]) + " / " + Percent(effective[1]) + " / "
                + Percent(effective[2]) + " / " + Percent(effective[3])
                + ", 환원율 " + Percent(GachaService.ExpectedRefundRate(_lure)));
            Assert.AreEqual(effective[3], shares[3], 0.002f);
            Assert.Greater(effective[3], 0.01f);
        }

        [Test]
        public void LegendRate_InterpolatesInsideCycle()
        {
            Assert.AreEqual(0.01f, GachaService.LegendRate(_lure, 0), 1e-5f);
            Assert.AreEqual(0.01f, GachaService.LegendRate(_lure, 49), 1e-5f, "51번째 뽑기까지는 기본 확률");
            Assert.AreEqual(0.03f, GachaService.LegendRate(_lure, 75), 1e-5f, "76번째는 1%와 5%의 중간");
            Assert.AreEqual(0.05f, GachaService.LegendRate(_lure, 100), 1e-5f, "101번째가 최고 확률");
            Assert.AreEqual(0.01f, GachaService.LegendRate(_lure, 100, false), 1e-5f);
        }

        [Test]
        public void NoCeiling_CounterWrapsAfter101WithoutGuarantee()
        {
            var service = new GachaService(new FixedRandom(0f));
            var counter = 0;
            for (var i = 0; i < 101; i++)
            {
                var gear = service.Pull(_lure, 1, ref counter)[0];
                Assert.AreNotEqual(ItemRarity.Legend, gear.Rarity);
            }

            Assert.AreEqual(0, counter, "101회 뒤 확정 없이 카운트만 처음으로 돌아간다");
            service.Pull(_lure, 1, ref counter);
            Assert.AreEqual(1, counter);
        }

        [Test]
        public void Legend_ResetsCounter()
        {
            var service = new GachaService(new FixedRandom(0.99999f));
            var counter = 40;
            var gear = service.Pull(_lure, 1, ref counter)[0];
            Assert.AreEqual(ItemRarity.Legend, gear.Rarity);
            Assert.AreEqual(0, counter);
        }

        [Test]
        public void Synchro_RollsAtBaseRateAndResetsCounter()
        {
            var synchroGear = GearData.Create("lure_synchro_test", "싱크로 루어", GearSlot.Lure, ItemRarity.Synchro);
            var gears = new List<GearData>(_lure.Gears) { synchroGear };
            var machine = ScriptableObject.CreateInstance<GachaMachineData>();
            try
            {
                machine.Configure("machine_synchro_test", "싱크로 테스트", GearSlot.Lure, 1000, gears);
                machine.SetRates(70f, 21f, 7f, 1.6f, 0.4f);

                var shares = SinglePullShares(machine, 100000, false);
                Assert.AreEqual(0.004f, shares[(int)ItemRarity.Synchro], 0.001f, "Synchro");

                var service = new GachaService(new FixedRandom(0.99999f));
                var counter = 40;
                var gear = service.Pull(machine, 1, ref counter)[0];
                Assert.AreEqual(ItemRarity.Synchro, gear.Rarity);
                Assert.AreEqual(0, counter, "싱크로도 Legend 카운트를 처음으로 돌린다");
                Assert.AreEqual(25f, synchroGear.RefundMultiplier, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(synchroGear);
                Object.DestroyImmediate(machine);
            }
        }

        [Test]
        public void TenPull_AlwaysContainsRareOrBetter()
        {
            AssertGuarantee(new GachaService(new FixedRandom(0f)), 10, 1, ItemRarity.Rare);
            AssertGuarantee(new GachaService(new SystemGachaRandom(Seed)), 10, 3000, ItemRarity.Rare);
        }

        [Test]
        public void HundredPull_AlwaysContainsEpicOrBetter()
        {
            AssertGuarantee(new GachaService(new FixedRandom(0f)), 100, 1, ItemRarity.Epic);
            AssertGuarantee(new GachaService(new SystemGachaRandom(Seed)), 100, 500, ItemRarity.Epic);
        }

        [Test]
        public void SinglePull_HasNoGuarantee()
        {
            var service = new GachaService(new FixedRandom(0f));
            var counter = 0;
            Assert.AreEqual(ItemRarity.Normal, service.Pull(_lure, 1, ref counter)[0].Rarity);
        }

        [Test]
        public void RefundValue_UsesMachinePriceTimesRarityMultiplier()
        {
            Assert.AreEqual(1500, _catalog.RefundValue(_catalog.FindGear("lure_rusty_magnet")));
            Assert.AreEqual(4000, _catalog.RefundValue(_catalog.FindGear("lure_neon_bait")));
            Assert.AreEqual(15000, _catalog.RefundValue(_catalog.FindGear("lure_drone_core")));
            Assert.AreEqual(75000, _catalog.RefundValue(_catalog.FindGear("lure_kraken_tentacle")));
            Assert.AreEqual(2250, _catalog.RefundValue(_catalog.FindGear("boat_patched_barge")));
            Assert.AreEqual(112500, _catalog.RefundValue(_catalog.FindGear("boat_mecha_fortress")));
        }

        [Test]
        public void ExpectedRefundRate_Is75PercentWithoutBoost()
        {
            _lure.SetBoost(101, 1000, 1f);
            Assert.AreEqual(0.746f, GachaService.ExpectedRefundRate(_lure), 0.001f);
        }

        [Test]
        public void Shop_InsufficientDollars_ChangesNothing()
        {
            var store = new MemoryKeyValueStore();
            var wallet = new MemoryWallet(4999);
            var shop = NewShop(store, wallet);
            Assert.IsFalse(shop.TryPull(_lure, 1, out _));
            Assert.AreEqual(4999, wallet.Balance);
            Assert.IsFalse(store.HasKey(GearInventory.SaveKey));
        }

        [Test]
        public void Shop_PullSpendsAndSavesImmediately()
        {
            var store = new MemoryKeyValueStore();
            var wallet = new MemoryWallet(1000000);
            var shop = NewShop(store, wallet);
            Assert.IsTrue(shop.TryPull(_lure, 100, out var result));
            Assert.AreEqual(500000, result.Cost);
            Assert.AreEqual(1000000, result.BalanceBefore);
            Assert.AreEqual(500000, wallet.Balance);
            Assert.AreEqual(100, result.Items.Count);
            Assert.Greater(store.SaveCount, 0);

            var reloaded = GearInventory.Load(store, _catalog);
            Assert.AreEqual(100, UnprocessedTotal(reloaded, null));
            Assert.AreEqual(GearInventory.HistoryLimit, reloaded.History.Count);
            Assert.AreEqual(shop.Inventory.Counter(_lure.Id), reloaded.Counter(_lure.Id));
        }

        [Test]
        public void Shop_MarksDuplicates()
        {
            var shop = NewShop(new MemoryKeyValueStore(), new MemoryWallet(100000), new FixedRandom(0f));
            Assert.IsTrue(shop.TryPull(_lure, 1, out var first));
            Assert.IsFalse(first.Items[0].Duplicate);
            Assert.IsTrue(shop.TryPull(_lure, 1, out var second));
            Assert.IsTrue(second.Items[0].Duplicate);
        }

        [Test]
        public void Shop_RefundAddsDollarsAndEquippedCannotBeRefunded()
        {
            var store = new MemoryKeyValueStore();
            var wallet = new MemoryWallet(10000);
            var shop = NewShop(store, wallet, new FixedRandom(0f));
            Assert.IsTrue(shop.TryPull(_lure, 2, out _));
            Assert.AreEqual(0, wallet.Balance);
            Assert.AreEqual(2, shop.Inventory.Count("lure_rusty_magnet", GearState.Unprocessed));

            Assert.IsTrue(shop.Equip("lure_rusty_magnet"));
            Assert.IsFalse(shop.TryRefund("lure_rusty_magnet", GearState.Equipped, 1, out _));
            Assert.IsTrue(shop.TryRefund("lure_rusty_magnet", GearState.Unprocessed, 1, out var dollars));
            Assert.AreEqual(1500, dollars);
            Assert.AreEqual(1500, wallet.Balance);
            Assert.AreEqual(1, shop.Inventory.Owned("lure_rusty_magnet"));
            Assert.IsFalse(shop.TryRefund("lure_rusty_magnet", GearState.Unprocessed, 1, out _));
        }

        [Test]
        public void Inventory_EquipSwapsAndFiltersBySlot()
        {
            var inventory = new GearInventory(_catalog);
            inventory.AddPulled(new List<GearData>
            {
                _catalog.FindGear("lure_rusty_magnet"),
                _catalog.FindGear("lure_neon_bait"),
                _catalog.FindGear("boat_patched_barge"),
                _catalog.FindGear("drone_old_watch")
            }, "test", 0);
            inventory.StoreAllUnprocessed();

            Assert.IsTrue(inventory.Equip("lure_rusty_magnet"));
            Assert.IsTrue(inventory.Equip("lure_neon_bait"));
            Assert.AreEqual("lure_neon_bait", inventory.EquippedId(GearSlot.Lure));
            Assert.AreEqual(1, inventory.Count("lure_rusty_magnet", GearState.Stored), "기존 장착품은 보관 중으로 돌아간다");

            var lures = inventory.List(GearState.Stored, GearSlot.Lure);
            Assert.AreEqual(1, lures.Count);
            Assert.AreEqual("lure_rusty_magnet", lures[0].Gear.Id);
            Assert.AreEqual(1, inventory.List(GearState.Stored, GearSlot.Boat).Count);
            Assert.AreEqual(1, inventory.List(GearState.Stored, GearSlot.Drone).Count);
            Assert.AreEqual(0, inventory.List(GearState.Stored, GearSlot.Helmet).Count);
            Assert.AreEqual(3, inventory.List(GearState.Stored).Count);
            Assert.AreEqual(1, inventory.List(GearState.Equipped, GearSlot.Lure).Count);
        }

        [Test]
        public void Inventory_StacksDuplicatesIntoOneEntry()
        {
            var inventory = new GearInventory(_catalog);
            var gear = _catalog.FindGear("suit_raincoat");
            inventory.AddPulled(new List<GearData> { gear, gear, gear }, "test", 0);
            var list = inventory.List(GearState.Unprocessed, GearSlot.Helmet);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(3, list[0].Count);
        }

        [Test]
        public void Migration_V2KeepsDollarsAndUpgradeLevels()
        {
            var store = new MemoryKeyValueStore();
            store.SetInt(SaveMigration.VersionKey, 2);
            store.SetInt(Progression.DollarKey, 123456);
            store.SetInt(Progression.LevelKey(UpgradeKind.Reel), 3);
            store.SetInt(Progression.LevelKey(UpgradeKind.Boat), 7);
            store.SetInt(Progression.LevelKey(UpgradeKind.Depth), 12);
            store.SetInt("BestCY", 9999);

            Assert.IsTrue(SaveMigration.Run(store));

            Assert.AreEqual(SaveMigration.CurrentVersion, store.GetInt(SaveMigration.VersionKey, 0));
            Assert.AreEqual(123456, store.GetInt(Progression.DollarKey, 0));
            Assert.AreEqual(3, store.GetInt(Progression.LevelKey(UpgradeKind.Reel), 0));
            Assert.AreEqual(7, store.GetInt(Progression.LevelKey(UpgradeKind.Boat), 0));
            Assert.AreEqual(12, store.GetInt(Progression.LevelKey(UpgradeKind.Depth), 0));
            Assert.AreEqual(9999, store.GetInt("BestCY", 0));
            Assert.IsTrue(store.HasKey(GearInventory.SaveKey));
            Assert.AreEqual(0, GearInventory.Load(store, _catalog).List(GearState.Unprocessed).Count);
        }

        [Test]
        public void Migration_CurrentVersionIsNoOp()
        {
            var store = new MemoryKeyValueStore();
            store.SetInt(SaveMigration.VersionKey, SaveMigration.CurrentVersion);
            store.SetInt(Progression.DollarKey, 500);
            Assert.IsFalse(SaveMigration.Run(store));
            Assert.AreEqual(500, store.GetInt(Progression.DollarKey, 0));
            Assert.AreEqual(0, store.SaveCount);
        }

        [Test]
        public void Migration_BrokenGachaSaveIsRebuiltWithoutTouchingDollars()
        {
            var store = new MemoryKeyValueStore();
            store.SetInt(SaveMigration.VersionKey, 2);
            store.SetInt(Progression.DollarKey, 777);
            store.SetString(GearInventory.SaveKey, "{not json");
            Assert.IsTrue(SaveMigration.Run(store));
            Assert.AreEqual(777, store.GetInt(Progression.DollarKey, 0));
            Assert.AreEqual(0, GearInventory.Load(store, _catalog).List(GearState.Unprocessed).Count);
        }

        [Test]
        public void Migration_LegacySaveStillResets()
        {
            var store = new MemoryKeyValueStore();
            store.SetInt(SaveMigration.VersionKey, 1);
            store.SetInt(Progression.DollarKey, 999);
            store.SetInt(Progression.LevelKey(UpgradeKind.Reel), 4);
            Assert.IsTrue(SaveMigration.Run(store));
            Assert.IsFalse(store.HasKey(Progression.DollarKey));
            Assert.IsFalse(store.HasKey(Progression.LevelKey(UpgradeKind.Reel)));
            Assert.AreEqual(SaveMigration.CurrentVersion, store.GetInt(SaveMigration.VersionKey, 0));
        }

        [Test]
        public void ShopView_BuildsWithoutErrors()
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
                area.SetParent(canvas.transform, false);
                area.sizeDelta = new Vector2(480f, 854f);
                var view = canvas.AddComponent<GachaShopView>();
                view.Build(area);
                Assert.IsFalse(view.IsOpen);
                Assert.AreEqual(6, view.Shop.Catalog.Machines.Count, "oripa_rewards.json: 루어·낚싯대·보트·드론·피셔·헬멧");
                Assert.IsNotNull(area.Find("BlackMarket/Machines/Viewport/Content"));
                Assert.IsNotNull(area.Find("BlackMarket/GachaInventory/List/Viewport/Content"));
                Assert.IsNotNull(area.Find("BlackMarket/TopBar/Inventory"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        GachaShop NewShop(IKeyValueStore store, IGachaWallet wallet, IGachaRandom random = null)
        {
            return new GachaShop(_catalog, new GachaService(random ?? new SystemGachaRandom(Seed)), wallet, store, () => 0L);
        }

        static float[] SinglePullShares(GachaMachineData machine, int pulls, bool boost)
        {
            var service = new GachaService(new SystemGachaRandom(Seed));
            var counts = new int[GachaService.RarityCount];
            var counter = 0;
            for (var i = 0; i < pulls; i++)
            {
                var gear = service.Pull(machine, 1, ref counter, boost)[0];
                counts[(int)gear.Rarity]++;
            }

            var shares = new float[counts.Length];
            for (var r = 0; r < counts.Length; r++)
            {
                shares[r] = counts[r] / (float)pulls;
            }

            return shares;
        }

        static void AssertShares(float[] shares, float normal, float rare, float epic, float legend, float tolerance)
        {
            Assert.AreEqual(normal, shares[0], tolerance, "Normal");
            Assert.AreEqual(rare, shares[1], tolerance, "Rare");
            Assert.AreEqual(epic, shares[2], tolerance, "Epic");
            Assert.AreEqual(legend, shares[3], tolerance, "Legend");
        }

        void AssertGuarantee(GachaService service, int count, int batches, ItemRarity floor)
        {
            var counter = 0;
            for (var b = 0; b < batches; b++)
            {
                var gears = service.Pull(_lure, count, ref counter);
                Assert.AreEqual(count, gears.Count);
                var best = ItemRarity.Normal;
                foreach (var gear in gears)
                {
                    if (gear.Rarity > best)
                    {
                        best = gear.Rarity;
                    }
                }

                Assert.GreaterOrEqual((int)best, (int)floor, count + "연 " + b + "번째 묶음");
            }
        }

        static int UnprocessedTotal(GearInventory inventory, GearSlot? slot)
        {
            var total = 0;
            foreach (var entry in inventory.List(GearState.Unprocessed, slot))
            {
                total += entry.Count;
            }

            return total;
        }

        static string Percent(float value)
        {
            return (value * 100f).ToString("0.00") + "%";
        }
    }
}
