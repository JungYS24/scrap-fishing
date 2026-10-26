using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScrapFishing.Data;
using ScrapFishing.Gacha;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScrapFishing.Tests
{
    public class GachaBannerTests
    {
        const int Seed = 20261026;

        GachaMachineData[] _machines;
        GachaCatalog _catalog;
        long _now;

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
            _machines = OripaCatalogJson.Load();
            Assert.IsNotNull(_machines, "Resources/Gacha/oripa_rewards.json");
            _catalog = new GachaCatalog(_machines);
            var set = OripaBannerJson.Load(_catalog);
            Assert.IsNotNull(set, "Resources/Gacha/oripa_banners.json");
            _catalog.SetBanners(set);
            _now = Kst(2026, 10, 26, 12, 0);
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

        GachaBanner Banner(string id)
        {
            var banner = _catalog.BannerSet.Find(id);
            Assert.IsNotNull(banner, id);
            return banner;
        }

        GachaShop NewShop(IKeyValueStore store, IGachaWallet wallet, IGachaRandom random = null)
        {
            return new GachaShop(_catalog, new GachaService(random ?? new SystemGachaRandom(Seed)), wallet, store, () => _now);
        }

        static long Kst(int year, int month, int day, int hour, int minute)
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-GachaBannerSet.KstOffsetHours).Ticks;
        }

        [Test]
        public void Json_TwentySixBannersAllShown()
        {
            var banners = _catalog.Banners;
            Assert.AreEqual(26, banners.Count);
            Assert.IsFalse(_catalog.BannerSet.RotationEnabled);
            Assert.AreEqual(1, banners.Count(b => b.Type == BannerType.Festival));
            Assert.AreEqual(1, banners.Count(b => b.Type == BannerType.Daily));
            Assert.AreEqual(6, banners.Count(b => b.Type == BannerType.Standard));
            Assert.AreEqual(18, banners.Count(b => b.Type == BannerType.Pickup));
            Assert.AreEqual(12, banners.Count(b => b.Pickup == PickupKind.Legend));
            Assert.AreEqual(6, banners.Count(b => b.Pickup == PickupKind.Synchro));

            foreach (var banner in banners)
            {
                Assert.IsFalse(banner.Offers(100), banner.Id);
                Assert.IsTrue(banner.PullCounts.All(c => c == 1 || c == GachaService.TenPull), banner.Id);
                switch (banner.Type)
                {
                    case BannerType.Standard:
                        Assert.AreEqual(banner.Machine.Price, banner.Price, banner.Id);
                        break;
                    case BannerType.Pickup:
                        Assert.AreSame(banner.Machine, _catalog.MachineOf(banner.Featured.Id), banner.Id);
                        Assert.AreEqual(banner.Pickup == PickupKind.Synchro ? 10000 : banner.Machine.Price, banner.Price, banner.Id);
                        Assert.AreEqual(banner.Pickup == PickupKind.Synchro ? ItemRarity.Synchro : ItemRarity.Legend, banner.Featured.Rarity, banner.Id);
                        break;
                }
            }

            var festival = Banner("banner_festival");
            Assert.AreEqual(9000, festival.Price);
            Assert.AreEqual(9000, GachaShop.CostOf(festival, 1));
            Assert.AreEqual(87000, GachaShop.CostOf(festival, 10), "10연 가격은 json pullPrices");
            Assert.AreEqual(30, festival.MaxPulls);
            Assert.IsTrue(festival.DailyLimit, "하루 30회");
            Assert.AreEqual(30, festival.PityAt);
            var daily = Banner("banner_daily");
            Assert.AreEqual(0, daily.Price);
            CollectionAssert.AreEqual(new[] { 1 }, daily.PullCounts);
            Assert.AreEqual(ItemRarity.Rare, daily.GuaranteeMin);
            Assert.AreEqual(7, daily.WeekdayPool.Count);
            Assert.IsNull(daily.WeekdayPool[DayOfWeek.Sunday], "일요일은 전체 머신");
        }

        [Test]
        public void NoHundredPullLeft()
        {
            Assert.IsNull(typeof(GachaService).GetField("HundredPull"));
            Assert.AreEqual(ItemRarity.Rare, GachaService.GuaranteeFor(100), "10연 이상은 레어 보장만");
            var rewards = Resources.Load<TextAsset>(OripaCatalogJson.Resource);
            StringAssert.DoesNotContain("\"count\": 100", rewards.text);
            var shop = NewShop(new MemoryKeyValueStore(), new MemoryWallet(10000000));
            foreach (var banner in _catalog.Banners)
            {
                Assert.AreEqual(BannerBlock.NotOffered, shop.Block(banner, 100), banner.Id);
            }
        }

        [Test]
        public void LegendPickup_FeaturedIsAbout75PercentOfLegends()
        {
            var banner = Banner("banner_lure_pickup_legend_01");
            var service = new GachaService(new SystemGachaRandom(Seed));
            var counter = 0;
            var legends = 0;
            var featured = 0;
            const int pulls = 300000;
            for (var i = 0; i < pulls; i++)
            {
                var gear = service.PullPickup(banner, 1, ref counter, false)[0];
                if (gear.Rarity == ItemRarity.Legend)
                {
                    legends++;
                    if (gear == banner.Featured)
                    {
                        featured++;
                    }
                }
            }

            Assert.AreEqual(0.016f, legends / (float)pulls, 0.0015f, "등급 확률은 상시와 같다");
            Assert.AreEqual(0.75f, featured / (float)legends, 0.03f, "픽업은 레전드 중 75%");
            Assert.AreEqual(0.012f, GachaService.FeaturedRate(banner, 0, false), 1e-5f);
        }

        [Test]
        public void LegendPickup_BoostKeeps75To25()
        {
            var banner = Banner("banner_boat_pickup_legend_02");
            var boosted = GachaService.FeaturedRate(banner, 100);
            Assert.AreEqual(0.05f * 0.75f, boosted, 1e-5f, "101번째: 레전드 5% 중 75%");
        }

        [Test]
        public void SynchroPickup_DoubleSynchroLessLegend()
        {
            var banner = Banner("banner_rod_pickup_synchro_01");
            var service = new GachaService(new SystemGachaRandom(Seed));
            var counts = new int[GachaService.RarityCount];
            var legendIds = new Dictionary<string, int>();
            var counter = 0;
            const int pulls = 300000;
            for (var i = 0; i < pulls; i++)
            {
                var gear = service.PullPickup(banner, 1, ref counter, false)[0];
                counts[(int)gear.Rarity]++;
                if (gear.Rarity == ItemRarity.Synchro)
                {
                    Assert.AreSame(banner.Featured, gear, "싱크로는 항상 픽업 장비");
                }
                else if (gear.Rarity == ItemRarity.Legend)
                {
                    legendIds[gear.Id] = legendIds.TryGetValue(gear.Id, out var n) ? n + 1 : 1;
                }
            }

            Assert.AreEqual(0.70f, counts[0] / (float)pulls, 0.01f, "Normal");
            Assert.AreEqual(0.21f, counts[1] / (float)pulls, 0.01f, "Rare");
            Assert.AreEqual(0.07f, counts[2] / (float)pulls, 0.005f, "Epic");
            Assert.AreEqual(0.012f, counts[3] / (float)pulls, 0.0015f, "Legend");
            Assert.AreEqual(0.008f, counts[4] / (float)pulls, 0.001f, "Synchro");
            Assert.AreEqual(2, legendIds.Count);
            foreach (var pair in legendIds)
            {
                Assert.AreEqual(0.5f, pair.Value / (float)counts[3], 0.06f, "레전드 2종은 반반 " + pair.Key);
            }

            var rates = new float[GachaService.RarityCount];
            GachaService.Rates(banner, 100, true, rates);
            Assert.AreEqual(0.05f, rates[(int)ItemRarity.Legend], 1e-5f, "천장 보정은 레전드만 올린다");
            Assert.AreEqual(0.008f, rates[(int)ItemRarity.Synchro], 1e-5f, "싱크로는 그대로");
        }

        [Test]
        public void SynchroPickup_RefundRateUsesMachinePrice()
        {
            var lure = GachaService.ExpectedRefundRate(Banner("banner_lure_pickup_synchro_01"), _catalog);
            var boat = GachaService.ExpectedRefundRate(Banner("banner_boat_pickup_synchro_01"), _catalog);
            Debug.Log("[Gacha] 싱크로 픽업 환원율 루어 " + lure + " / 보트 " + boat);
            Assert.AreEqual(0.42f, lure, 0.03f);
            Assert.AreEqual(0.63f, boat, 0.04f);
        }

        [Test]
        public void Pickup_SharesMachineCounter()
        {
            var store = new MemoryKeyValueStore();
            var wallet = new MemoryWallet(1000000);
            var shop = NewShop(store, wallet, new FixedRandom(0f));
            var lure = _catalog.FindMachine("machine_lure");
            shop.Inventory.SetCounter(lure.Id, 40);
            Assert.IsTrue(shop.TryPull(Banner("banner_lure_pickup_legend_01"), 1, out var result));
            Assert.AreEqual(41, shop.Inventory.Counter(lure.Id));
            Assert.AreEqual(5000, result.Cost);
            Assert.AreSame(lure, result.Machine);

            Assert.IsTrue(shop.TryPull(Banner("banner_lure_pickup_synchro_01"), GachaService.TenPull, out result));
            Assert.AreEqual(100000, result.Cost, "싱크로 픽업 $10,000 × 10");
            Assert.AreEqual(GachaService.TenPull, result.Items.Count);
            Assert.IsTrue(result.Items.Any(i => i.Gear.Rarity >= ItemRarity.Rare), "10연 레어 보장");
            Assert.AreEqual(51, shop.Inventory.Counter(lure.Id));
        }

        [Test]
        public void Standard_BannerMatchesMachinePull()
        {
            var shop = NewShop(new MemoryKeyValueStore(), new MemoryWallet(100000), new FixedRandom(0f));
            Assert.IsTrue(shop.TryPull(Banner("banner_boat_standard"), 1, out var result));
            Assert.AreEqual(7500, result.Cost);
            Assert.AreEqual(1, shop.Inventory.Counter("machine_boat"));
            Assert.AreEqual("banner_boat_standard", result.Banner.Id);
        }

        [Test]
        public void Daily_SecondPullSameKstDayRejected()
        {
            var store = new MemoryKeyValueStore();
            var wallet = new MemoryWallet(0);
            var shop = NewShop(store, wallet);
            var daily = Banner("banner_daily");

            Assert.AreEqual(BannerBlock.None, shop.Block(daily, 1), "무료라 잔액 0이어도 뽑는다");
            Assert.IsTrue(shop.TryPull(daily, 1, out var result));
            Assert.AreEqual(0, result.Cost);
            Assert.AreEqual(0, wallet.Balance);
            Assert.AreEqual(BannerBlock.DailyDone, shop.Block(daily, 1));
            Assert.IsFalse(shop.TryPull(daily, 1, out _));

            _now = Kst(2026, 10, 27, 4, 59);
            Assert.IsFalse(shop.TryPull(daily, 1, out _), "다음 날 04:59는 아직 같은 날");
            _now = Kst(2026, 10, 27, 5, 0);
            Assert.IsTrue(shop.TryPull(daily, 1, out _), "05:00에 갱신");
            Assert.IsFalse(shop.TryPull(daily, 1, out _));

            var reloaded = NewShop(store, wallet);
            Assert.IsFalse(reloaded.DailyAvailable(daily), "저장된 날짜를 다시 읽는다");
        }

        [Test]
        public void Daily_DayBoundaryIsKst0500()
        {
            var shop = NewShop(new MemoryKeyValueStore(), new MemoryWallet(0));
            _now = Kst(2026, 10, 27, 4, 59);
            Assert.AreEqual(new DateTime(2026, 10, 26), shop.DailyDay());
            Assert.AreEqual(DayOfWeek.Monday, shop.DailyDay().DayOfWeek);
            _now = Kst(2026, 10, 27, 5, 0);
            Assert.AreEqual(new DateTime(2026, 10, 27), shop.DailyDay());
            Assert.AreEqual(DayOfWeek.Tuesday, shop.DailyDay().DayOfWeek);
            // UTC 20:00 = KST 다음 날 05:00
            _now = new DateTime(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc).Ticks;
            Assert.AreEqual(new DateTime(2026, 10, 28), shop.DailyDay());
        }

        [Test]
        public void Daily_WeekdayPoolAndRareOrBetter()
        {
            var expected = new Dictionary<DayOfWeek, string>
            {
                { DayOfWeek.Monday, "machine_lure" },
                { DayOfWeek.Tuesday, "machine_rod" },
                { DayOfWeek.Wednesday, "machine_boat" },
                { DayOfWeek.Thursday, "machine_drone" },
                { DayOfWeek.Friday, "machine_fisher" },
                { DayOfWeek.Saturday, "machine_helmet" },
                { DayOfWeek.Sunday, null },
            };
            var daily = Banner("banner_daily");
            var store = new MemoryKeyValueStore();
            var shop = NewShop(store, new MemoryWallet(0));
            var sundayMachines = new HashSet<string>();
            // 2026-10-26(월)부터 8주. 매일 받은 장비가 그 요일 머신이고 레어 이상이다.
            for (var day = 0; day < 56; day++)
            {
                _now = Kst(2026, 10, 26, 12, 0) + TimeSpan.FromDays(day).Ticks;
                var weekday = shop.DailyDay().DayOfWeek;
                Assert.AreEqual(expected[weekday], shop.DailyMachine(daily)?.Id, weekday.ToString());
                Assert.IsTrue(shop.TryPull(daily, 1, out var result), "day " + day);
                var gear = result.Items.Single().Gear;
                Assert.GreaterOrEqual((int)gear.Rarity, (int)ItemRarity.Rare, gear.Id);
                var machine = _catalog.MachineOf(gear.Id);
                if (expected[weekday] != null)
                {
                    Assert.AreEqual(expected[weekday], machine.Id, weekday + " " + gear.Id);
                }
                else
                {
                    sundayMachines.Add(machine.Id);
                }

                Assert.AreSame(machine, result.Machine, "연출 영상은 나온 장비의 머신");
            }

            Assert.Greater(sundayMachines.Count, 1, "일요일은 전체 풀");

            var fixedShop = NewShop(new MemoryKeyValueStore(), new MemoryWallet(0), new FixedRandom(0f));
            Assert.IsTrue(fixedShop.TryPull(daily, 1, out var low));
            Assert.AreEqual(ItemRarity.Rare, low.Items[0].Gear.Rarity, "가장 낮은 난수도 레어");
        }

        [Test]
        public void Daily_DoesNotTouchMachineCounters()
        {
            var shop = NewShop(new MemoryKeyValueStore(), new MemoryWallet(0), new FixedRandom(0f));
            shop.Inventory.SetCounter("machine_lure", 30);
            Assert.IsTrue(shop.TryPull(Banner("banner_daily"), 1, out _));
            Assert.AreEqual(30, shop.Inventory.Counter("machine_lure"));
            foreach (var machine in _machines.Where(m => m.Id != "machine_lure"))
            {
                Assert.AreEqual(0, shop.Inventory.Counter(machine.Id), machine.Id);
            }
        }

        [Test]
        public void Festival_ThirtyPullLimitAndTenPullBlockedAtNine()
        {
            var store = new MemoryKeyValueStore();
            var wallet = new MemoryWallet(10000000);
            var shop = NewShop(store, wallet);
            var festival = Banner("banner_festival");

            Assert.AreEqual(30, shop.FestivalRemaining(festival));
            Assert.IsTrue(shop.TryPull(festival, 1, out var one));
            Assert.AreEqual(9000, one.Cost);
            var balance = wallet.Balance;
            Assert.IsTrue(shop.TryPull(festival, 10, out var ten));
            Assert.AreEqual(87000, ten.Cost);
            Assert.AreEqual(balance - 87000, wallet.Balance);
            Assert.IsTrue(shop.TryPull(festival, 10, out _));
            Assert.AreEqual(9, shop.FestivalRemaining(festival));
            Assert.AreEqual(BannerBlock.LimitReached, shop.Block(festival, 10));
            var before = wallet.Balance;
            Assert.IsFalse(shop.TryPull(festival, 10, out _));
            Assert.AreEqual(before, wallet.Balance);
            Assert.AreEqual(BannerBlock.None, shop.Block(festival, 1));

            for (var i = 0; i < 9; i++)
            {
                Assert.IsTrue(shop.TryPull(festival, 1, out _), "남은 " + (9 - i));
            }

            Assert.AreEqual(0, shop.FestivalRemaining(festival));
            Assert.AreEqual(BannerBlock.LimitReached, shop.Block(festival, 1));
            Assert.IsFalse(shop.TryPull(festival, 1, out _));
            Assert.AreEqual(30, NewShop(store, wallet).FestivalPulls(festival), "저장된 횟수를 다시 읽는다");

            // 다음 날 05:00(KST) 전까지는 그대로, 05:00부터 다시 30회.
            _now = Kst(2026, 10, 27, 4, 59);
            Assert.AreEqual(0, shop.FestivalRemaining(festival));
            _now = Kst(2026, 10, 27, 5, 0);
            Assert.AreEqual(30, shop.FestivalRemaining(festival), "하루가 지나면 다시 30회");
            Assert.AreEqual(BannerBlock.None, shop.Block(festival, 10));
            Assert.IsTrue(shop.TryPull(festival, 10, out _));
            Assert.AreEqual(20, NewShop(store, wallet).FestivalRemaining(festival));
        }

        [Test]
        public void Festival_PityResetsEachDay()
        {
            var store = new MemoryKeyValueStore();
            var festival = Banner("banner_festival");
            var high = NewShop(store, new MemoryWallet(10000000), new FixedRandom(0.99999f));
            Assert.IsTrue(high.TryPull(festival, 1, out _));
            Assert.IsTrue(high.FestivalHadHigh(festival), "오늘은 레전드 이상을 받았다");

            // 다음 날은 확정 기록도 새로 시작한다: 가장 낮은 난수로 30번 뽑으면 30번째가 레전드 이상.
            _now = Kst(2026, 10, 27, 6, 0);
            var low = NewShop(store, new MemoryWallet(10000000), new FixedRandom(0f));
            Assert.IsFalse(low.FestivalHadHigh(festival));
            var last = ItemRarity.Normal;
            for (var i = 0; i < 30; i++)
            {
                Assert.IsTrue(low.TryPull(festival, 1, out var result), "#" + (i + 1));
                last = result.Items[0].Gear.Rarity;
            }

            Assert.GreaterOrEqual((int)last, (int)ItemRarity.Legend, "그날 30번째 확정");
        }

        [Test]
        public void Discount_OnlyWhenCheaperThanSinglePulls()
        {
            var festival = Banner("banner_festival");
            Assert.AreEqual(90000, GachaShop.ListPriceOf(festival, 10));
            Assert.AreEqual(0, GachaShop.DiscountPercent(festival, 1));
            Assert.AreEqual(3, GachaShop.DiscountPercent(festival, 10), "10연 $87,000 = 1회 × 10($90,000)보다 3% 할인");
            Assert.AreEqual(0, GachaShop.DiscountPercent(Banner("banner_lure_standard"), 10), "상시 머신은 할인 없음");

            var cheap = new GachaBanner { Id = "test", Type = BannerType.Festival, Price = 10000 };
            cheap.PullPrices[10] = 90000;
            Assert.AreEqual(10, GachaShop.DiscountPercent(cheap, 10));
        }

        [Test]
        public void Festival_ThirtiethPullIsLegendOrBetter()
        {
            var store = new MemoryKeyValueStore();
            var shop = NewShop(store, new MemoryWallet(10000000), new FixedRandom(0f));
            var festival = Banner("banner_festival");
            shop.Inventory.SetCounter("machine_lure", 12);
            var all = new List<GearData>();
            for (var batch = 0; batch < 3; batch++)
            {
                Assert.IsTrue(shop.TryPull(festival, 10, out var result));
                all.AddRange(result.Items.Select(i => i.Gear));
                Assert.AreSame(GachaShop.VideoMachineOf(_catalog, result.Items.Select(i => i.Gear).ToList()), result.Machine);
            }

            Assert.AreEqual(30, all.Count);
            for (var i = 0; i < 29; i++)
            {
                Assert.Less((int)all[i].Rarity, (int)ItemRarity.Legend, "가장 낮은 난수에서는 30번째 전까지 레전드 없음 #" + (i + 1));
            }

            Assert.GreaterOrEqual((int)all[29].Rarity, (int)ItemRarity.Legend, "30번째 확정");
            Assert.AreEqual(ItemRarity.Rare, all[9].Rarity, "10연 레어 보장");
            Assert.IsTrue(shop.FestivalHadHigh(festival));
            Assert.AreEqual(12, shop.Inventory.Counter("machine_lure"), "머신 천장 카운터는 그대로");
            foreach (var machine in _machines.Where(m => m.Id != "machine_lure"))
            {
                Assert.AreEqual(0, shop.Inventory.Counter(machine.Id), machine.Id);
            }
        }

        [Test]
        public void Festival_NoPityAfterEarlierLegend()
        {
            var store = new MemoryKeyValueStore();
            var festival = Banner("banner_festival");
            var high = NewShop(store, new MemoryWallet(10000000), new FixedRandom(0.99999f));
            Assert.IsTrue(high.TryPull(festival, 1, out var first));
            Assert.AreEqual(ItemRarity.Synchro, first.Items[0].Gear.Rarity);
            Assert.IsTrue(high.FestivalHadHigh(festival));

            var low = NewShop(store, new MemoryWallet(10000000), new FixedRandom(0f));
            for (var i = 0; i < 29; i++)
            {
                Assert.IsTrue(low.TryPull(festival, 1, out var result));
                Assert.AreEqual(ItemRarity.Normal, result.Items[0].Gear.Rarity, "이미 레전드 이상을 받았으면 확정 없음 #" + (i + 2));
            }
        }

        [Test]
        public void Festival_RatesMatchBannerAcrossAllMachines()
        {
            var festival = Banner("banner_festival");
            var rates = new float[GachaService.RarityCount];
            GachaService.Rates(festival, 0, false, rates);
            var service = new GachaService(new SystemGachaRandom(Seed));
            var counts = new int[GachaService.RarityCount];
            var machines = new HashSet<string>();
            const int pulls = 200000;
            for (var i = 0; i < pulls; i++)
            {
                var gear = service.PullPool(_catalog.AllGears, rates, 1)[0];
                counts[(int)gear.Rarity]++;
                machines.Add(_catalog.MachineOf(gear.Id).Id);
            }

            Assert.AreEqual(0.6857f, counts[0] / (float)pulls, 0.01f, "Normal");
            Assert.AreEqual(0.2057f, counts[1] / (float)pulls, 0.01f, "Rare");
            Assert.AreEqual(0.0686f, counts[2] / (float)pulls, 0.005f, "Epic");
            Assert.AreEqual(0.032f, counts[3] / (float)pulls, 0.002f, "Legend");
            Assert.AreEqual(0.008f, counts[4] / (float)pulls, 0.001f, "Synchro");
            Assert.AreEqual(6, machines.Count);
        }

        [Test]
        public void Festival_FailedSpendRestoresKeys()
        {
            var store = new MemoryKeyValueStore();
            var shop = NewShop(store, new MemoryWallet(8999));
            var festival = Banner("banner_festival");
            Assert.AreEqual(BannerBlock.NoDollars, shop.Block(festival, 1));
            Assert.IsFalse(shop.TryPull(festival, 1, out _));
            Assert.IsFalse(store.HasKey(GachaShop.FestivalPullsKey + festival.Id));
            Assert.AreEqual(30, shop.FestivalRemaining(festival));
        }

        [Test]
        public void History_RecordsGearMachineAndBanner()
        {
            var store = new MemoryKeyValueStore();
            var shop = NewShop(store, new MemoryWallet(10000000));
            Assert.IsTrue(shop.TryPull(Banner("banner_festival"), 10, out _));
            Assert.IsTrue(shop.TryPull(Banner("banner_daily"), 1, out _));
            Assert.IsTrue(shop.TryPull(Banner("banner_drone_pickup_legend_01"), 1, out _));
            Assert.IsTrue(shop.TryPull(Banner("banner_rod_standard"), 1, out _));
            Assert.IsTrue(shop.TryPull(_catalog.FindMachine("machine_lure"), 1, out _));

            var history = GearInventory.Load(store, _catalog).History;
            Assert.AreEqual(14, history.Count);
            foreach (var record in history)
            {
                Assert.AreEqual(_catalog.MachineOf(record.gear).Id, record.machine, "기록의 머신은 장비의 머신 " + record.gear);
            }

            Assert.IsTrue(history.Take(10).All(r => r.banner == "banner_festival"));
            Assert.AreEqual("banner_daily", history[10].banner);
            Assert.AreEqual("banner_drone_pickup_legend_01", history[11].banner);
            Assert.AreEqual("banner_rod_standard", history[12].banner);
            Assert.IsTrue(string.IsNullOrEmpty(history[13].banner), "머신을 직접 뽑으면 배너 없음(JsonUtility는 null을 빈 문자열로 저장)");
        }

        [Test]
        public void History_OldSaveWithoutBannerFieldLoads()
        {
            var store = new MemoryKeyValueStore();
            store.SetString(GearInventory.SaveKey,
                "{\"version\":1,\"gear\":[{\"id\":\"lure_rare_01\",\"unprocessed\":1,\"stored\":0}],\"equipped\":[\"\",\"\",\"\",\"\",\"\",\"\"],"
                + "\"counters\":[{\"machine\":\"machine_lure\",\"count\":12}],"
                + "\"history\":[{\"machine\":\"machine_lure\",\"gear\":\"lure_rare_01\",\"rarity\":1,\"time\":5}]}");
            var inventory = GearInventory.Load(store, _catalog);
            Assert.AreEqual(1, inventory.History.Count);
            Assert.AreEqual("machine_lure", inventory.History[0].machine);
            Assert.IsTrue(string.IsNullOrEmpty(inventory.History[0].banner));
            Assert.AreEqual(12, inventory.Counter("machine_lure"));
            Assert.AreEqual(1, inventory.Count("lure_rare_01", GearState.Unprocessed));
        }

        [Test]
        public void Disclaimer_IsPerBanner()
        {
            StringAssert.Contains("이 배너 기준", GachaConfirmPopup.DisclaimerFor(Banner("banner_lure_standard")));
            StringAssert.Contains("천장 보정", GachaConfirmPopup.DisclaimerFor(Banner("banner_lure_pickup_synchro_01")));
            StringAssert.Contains("레어 이상 확정", GachaConfirmPopup.DisclaimerFor(Banner("banner_daily")));
            StringAssert.Contains("30회째 레전드 이상 확정", GachaConfirmPopup.DisclaimerFor(Banner("banner_festival")));
        }

        [Test]
        public void VideoMachine_IsFirstHighestGrade()
        {
            var gears = new[] { "lure_normal_01", "boat_epic_01", "rod_epic_01", "drone_rare_01" }.Select(_catalog.FindGear).ToList();
            Assert.AreEqual("machine_boat", GachaShop.VideoMachineOf(_catalog, gears).Id);
            gears.Add(_catalog.FindGear("helmet_legend_01"));
            Assert.AreEqual("machine_helmet", GachaShop.VideoMachineOf(_catalog, gears).Id);
        }

        [Test]
        public void Catalog_WithoutBannerFileFallsBackToStandardBanners()
        {
            var catalog = new GachaCatalog(_machines);
            Assert.AreEqual(6, catalog.Banners.Count);
            Assert.IsTrue(catalog.Banners.All(b => b.Type == BannerType.Standard && b.Price == b.Machine.Price));
        }
    }
}
