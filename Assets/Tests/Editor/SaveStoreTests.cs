using System.Linq;
using NUnit.Framework;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Fishing;
using ScrapFishing.Gacha;
using ScrapFishing.UI;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class SaveStoreTests
    {
        ISaveStore _previous;
        MemorySaveStore _store;

        [SetUp]
        public void UseFreshStore()
        {
            _previous = SaveStore.Current;
            _store = new MemorySaveStore();
            SaveStore.Use(_store);
            VolumeSettings.Reload();
        }

        [TearDown]
        public void RestoreStore()
        {
            SaveStore.Use(_previous);
            // 언어·음량은 static에 기억해 두므로 원래 저장소 값으로 다시 읽는다.
            Loc.Reload();
            VolumeSettings.Reload();
        }

        [Test]
        public void GameCode_WritesThroughSaveStore_WithSameKeys()
        {
            Progression.AddDollars(1234);
            Assert.AreEqual("1234", _store.GetString(Progression.DollarKey, null), "키 이름(Dollar)과 정수 형식 그대로");
            VolumeSettings.Set(VolumeChannel.Bgm, 35);
            Assert.AreEqual(35, _store.GetInt(VolumeSettings.BgmKey, -1));
            Loc.SetLanguage(GameLanguage.En, true);
            Assert.AreEqual("en", _store.GetString(Loc.PrefKey, null));
            Loc.SetLanguage(GameLanguage.Ko, true);
            Assert.Greater(_store.SaveCount, 0, "저장 요청이 저장소로 간다");
        }

        [Test]
        public void MemoryStore_RoundTripsAndDeletes()
        {
            _store.SetInt("i", -7);
            _store.SetFloat("f", 0.25f);
            _store.SetString("s", "가\"b");
            Assert.AreEqual(-7, _store.GetInt("i", 0));
            Assert.AreEqual(0.25f, _store.GetFloat("f", 0f));
            Assert.AreEqual("가\"b", _store.GetString("s", null));
            Assert.AreEqual(5, _store.GetInt("missing", 5));
            _store.DeleteKey("i");
            Assert.IsFalse(_store.HasKey("i"));
            _store.DeleteAll();
            Assert.IsFalse(_store.HasKey("s"));
        }

        [Test]
        public void EstimateBytes_CountsJsonWithEscapesInUtf8()
        {
            _store.SetString("a", "가");
            // {"a":"가"} = { "a" : "가" } = 1+3+1+(2+3)+1 바이트(가는 UTF-8 3바이트)
            Assert.AreEqual(11, _store.EstimateJsonBytes());
            SaveStore.SetString("a", "x\"y");
            Assert.AreEqual(SaveStore.DebugEstimateBytes(), _store.EstimateJsonBytes(), "디버그 함수는 이번 실행에서 쓴 키 기준");
        }

        // CrazyGames Data 모듈 한도는 1MB. 모든 키를 가장 큰 값으로 채운 최악의 저장 크기를 계산해 한도 안인지 본다.
        // 오리파 기록은 GearInventory.HistoryLimit(50)개까지만 남고, 나머지 키는 장비·머신·배너 수만큼만 생긴다.
        [Test]
        public void WorstCaseSave_FitsCrazyGamesDataLimit()
        {
            var catalog = GachaCatalog.LoadDefault();
            Progression.AddDollars(int.MaxValue);
            foreach (UpgradeKind kind in System.Enum.GetValues(typeof(UpgradeKind)))
            {
                SaveStore.SetInt(Progression.LevelKey(kind), Progression.MaxUpgradeLevel);
            }

            SaveStore.SetInt(RodStats.LevelKey, int.MaxValue);
            SaveStore.SetInt("BestCY", int.MaxValue);
            SaveStore.SetInt("SoundOn", 0);
            VolumeSettings.Set(VolumeChannel.Master, 100);
            VolumeSettings.Set(VolumeChannel.Bgm, 100);
            VolumeSettings.Set(VolumeChannel.Sfx, 100);
            Loc.SetLanguage(GameLanguage.En, true);
            SaveStore.SetInt(FirstPlayGuide.Key, 2);
            SaveStore.SetInt(SettingsView.EventCouponUsedKey, 1);
            SaveMigration.Run(SaveStore.Current, catalog);
            foreach (var machine in catalog.Machines)
            {
                GachaVideoConfig.MarkSeen(machine.Id);
            }

            foreach (var banner in catalog.Banners)
            {
                foreach (var prefix in new[] { GachaShop.DailyDayKey, GachaShop.FestivalPullsKey, GachaShop.FestivalHighKey, GachaShop.FestivalDayKey })
                {
                    SaveStore.SetInt(prefix + banner.Id, int.MinValue);
                }
            }

            var data = new GachaSaveData();
            var longestGear = catalog.AllGears.OrderByDescending(g => g.Id.Length).First().Id;
            var longestMachine = catalog.Machines.OrderByDescending(m => m.Id.Length).First().Id;
            var longestBanner = catalog.Banners.OrderByDescending(b => b.Id.Length).First().Id;
            foreach (var gear in catalog.AllGears)
            {
                data.gear.Add(new GearStack { id = gear.Id, unprocessed = int.MaxValue, stored = int.MaxValue });
            }

            for (var i = 0; i < data.equipped.Length; i++)
            {
                data.equipped[i] = longestGear;
            }

            foreach (var machine in catalog.Machines)
            {
                data.counters.Add(new GachaCounter { machine = machine.Id, count = int.MaxValue });
            }

            for (var i = 0; i < GearInventory.HistoryLimit * 3; i++)
            {
                data.history.Add(new GachaRecord { machine = longestMachine, banner = longestBanner, gear = longestGear, rarity = 9, time = long.MaxValue });
            }

            var inventory = new GearInventory(catalog, data);
            Assert.LessOrEqual(inventory.History.Count, GearInventory.HistoryLimit, "오리파 기록은 상한까지만 남는다");
            inventory.Write(SaveStore.Current);

            var bytes = SaveStore.DebugEstimateBytes();
            Assert.AreEqual(_store.EstimateJsonBytes(), bytes);
            Debug.Log("[SaveSize] worst case " + bytes + " bytes (" + (bytes * 100f / SaveStore.DataLimitBytes).ToString("0.00")
                + "% of 1MB), keys " + _store.Entries.Count() + ", gear " + catalog.AllGears.Count + ", banners " + catalog.Banners.Count);
            Assert.Less(bytes, SaveStore.DataLimitBytes / 10, "최악이어도 한도의 10% 아래");
        }
    }
}
