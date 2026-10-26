using System;
using System.Collections.Generic;
using ScrapFishing.Core;
using ScrapFishing.Data;

namespace ScrapFishing.Gacha
{
    public interface IGachaWallet
    {
        int Balance { get; }
        bool TrySpend(int amount);
        void Add(int amount);
    }

    public sealed class ProgressionWallet : IGachaWallet
    {
        public static readonly ProgressionWallet Instance = new ProgressionWallet();

        public int Balance => Progression.Dollars;
        public bool TrySpend(int amount) => Progression.TrySpend(amount);
        public void Add(int amount) => Progression.AddDollars(amount);
    }

    public sealed class MemoryWallet : IGachaWallet
    {
        public int Balance { get; private set; }

        public MemoryWallet(int balance)
        {
            Balance = Math.Max(0, balance);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Balance < amount)
            {
                return false;
            }

            Balance -= amount;
            return true;
        }

        public void Add(int amount)
        {
            if (amount > 0)
            {
                Balance += amount;
            }
        }
    }

    public readonly struct PulledGear
    {
        public readonly GearData Gear;
        public readonly bool Duplicate;

        public PulledGear(GearData gear, bool duplicate)
        {
            Gear = gear;
            Duplicate = duplicate;
        }
    }

    public sealed class GachaPullResult
    {
        public GachaBanner Banner;
        // 연출 영상 머신. standard·pickup은 배너 머신, daily·festival은 이번 결과의 최고 등급(같으면 첫 번째) 장비 머신.
        public GachaMachineData Machine;
        public int Count;
        public int Cost;
        public int BalanceBefore;
        public int BalanceAfter;
        public readonly List<PulledGear> Items = new List<PulledGear>();
    }

    // 배너를 지금 뽑을 수 없는 이유.
    public enum BannerBlock
    {
        None,
        NotOffered,
        DailyDone,
        LimitReached,
        NoDollars,
    }

    // 확인 버튼을 누른 순간 결과를 계산해 달러 차감과 함께 저장한다. 연출은 그 뒤에 시작한다.
    public sealed class GachaShop
    {
        // 배너 저장 키. GachaSave(장비·머신 카운터)와 따로 둔다. 뒤에 배너 id가 붙는다.
        public const string DailyDayKey = "GachaDailyDay_";
        public const string FestivalPullsKey = "GachaFestivalPulls_";
        public const string FestivalHighKey = "GachaFestivalHigh_";
        // 하루 제한 축제: 횟수·확정 기록이 어느 날(KST 05:00 기준) 것인지.
        public const string FestivalDayKey = "GachaFestivalDay_";

        readonly GachaService _service;
        readonly IGachaWallet _wallet;
        readonly IKeyValueStore _store;
        readonly Func<long> _clock;

        public GachaCatalog Catalog { get; }
        public GearInventory Inventory { get; }
        public int Balance => _wallet.Balance;

        public GachaShop(GachaCatalog catalog, GachaService service, IGachaWallet wallet, IKeyValueStore store, Func<long> clock = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? (() => DateTime.UtcNow.Ticks);
            Inventory = GearInventory.Load(store, catalog);
        }

        // 실제 게임용 상점. 장착 칸이 빈 슬롯은 노멀 1번으로 채워 저장한다(첫 시작이면 6종 모두).
        public static GachaShop CreateDefault()
        {
            var shop = new GachaShop(GachaCatalog.LoadDefault(), new GachaService(new SystemGachaRandom()), ProgressionWallet.Instance, PlayerPrefsStore.Instance);
            shop.EnsureStarterGear();
            return shop;
        }

        public bool EnsureStarterGear()
        {
            return Commit(Inventory.EnsureStarterGear());
        }

        public static int CostOf(GachaMachineData machine, int count)
        {
            return machine != null && count > 0 ? machine.Price * count : 0;
        }

        public bool CanAfford(GachaMachineData machine, int count)
        {
            var cost = CostOf(machine, count);
            return cost > 0 && _wallet.Balance >= cost;
        }

        public bool TryPull(GachaMachineData machine, int count, out GachaPullResult result)
        {
            return TryPullMachine(machine, count, null, out result);
        }

        bool TryPullMachine(GachaMachineData machine, int count, string bannerId, out GachaPullResult result)
        {
            result = null;
            if (machine == null || count <= 0 || !CanAfford(machine, count))
            {
                return false;
            }

            var before = _wallet.Balance;
            var snapshot = Inventory.ToJson();
            var counter = Inventory.Counter(machine.Id);
            var gears = _service.Pull(machine, count, ref counter);
            var pulled = new List<PulledGear>(gears.Count);
            var seen = new HashSet<string>();
            for (var i = 0; i < gears.Count; i++)
            {
                var gear = gears[i];
                if (gear == null)
                {
                    continue;
                }

                pulled.Add(new PulledGear(gear, Inventory.Owned(gear.Id) > 0 || !seen.Add(gear.Id)));
            }

            Inventory.SetCounter(machine.Id, counter);
            Inventory.AddPulled(gears, machine.Id, _clock(), bannerId);
            Inventory.Write(_store);

            // ProgressionWallet은 차감과 함께 PlayerPrefs.Save()를 부르므로 가챠 저장도 같이 기록된다.
            if (!_wallet.TrySpend(CostOf(machine, count)))
            {
                Inventory.RestoreJson(snapshot);
                Inventory.Write(_store);
                return false;
            }

            _store.Save();
            result = new GachaPullResult
            {
                Machine = machine,
                Count = count,
                Cost = CostOf(machine, count),
                BalanceBefore = before,
                BalanceAfter = _wallet.Balance
            };
            result.Items.AddRange(pulled);
            return true;
        }

        public DateTime UtcNow => new DateTime(Math.Max(0L, _clock()), DateTimeKind.Utc);

        public static int CostOf(GachaBanner banner, int count)
        {
            if (banner == null || count <= 0)
            {
                return 0;
            }

            return banner.PullPrices.TryGetValue(count, out var price) ? Math.Max(0, price) : ListPriceOf(banner, count);
        }

        // 1회 가격 × 횟수(할인 전 가격).
        public static int ListPriceOf(GachaBanner banner, int count)
        {
            return banner != null && count > 0 ? Math.Max(0, banner.Price) * count : 0;
        }

        // 1회 가격 × 횟수보다 싸면 할인율(%, 반올림). 같거나 비싸면 0.
        public static int DiscountPercent(GachaBanner banner, int count)
        {
            var list = ListPriceOf(banner, count);
            var cost = CostOf(banner, count);
            return list > 0 && cost < list ? (int)Math.Round((1.0 - cost / (double)list) * 100.0) : 0;
        }

        public BannerBlock Block(GachaBanner banner, int count)
        {
            if (banner == null || count <= 0 || !banner.Offers(count))
            {
                return BannerBlock.NotOffered;
            }

            switch (banner.Type)
            {
                case BannerType.Daily:
                    if (!DailyAvailable(banner))
                    {
                        return BannerBlock.DailyDone;
                    }

                    break;
                case BannerType.Festival:
                    if (banner.MaxPulls > 0 && FestivalRemaining(banner) < count)
                    {
                        return BannerBlock.LimitReached;
                    }

                    break;
            }

            var cost = CostOf(banner, count);
            return cost > 0 && _wallet.Balance < cost ? BannerBlock.NoDollars : BannerBlock.None;
        }

        // 데일리: 리셋 시각(KST 05:00) 기준 날짜. 마지막으로 받은 날짜만 저장하므로 기기 시계를 돌리는 것은 막지 않는다.
        public DateTime DailyDay()
        {
            return Catalog.BannerSet.LocalDay(UtcNow);
        }

        public bool DailyAvailable(GachaBanner banner)
        {
            return _store.GetInt(DailyDayKey + banner.Id, 0) != DayStamp(DailyDay());
        }

        // 오늘 요일의 풀 머신. null이면 전체 머신(또는 요일 설정 없음).
        public GachaMachineData DailyMachine(GachaBanner banner)
        {
            return banner.WeekdayPool.TryGetValue(DailyDay().DayOfWeek, out var machine) ? machine : null;
        }

        // 하루 제한 축제는 저장된 기록이 오늘 것이 아니면 0회(확정 기록도 없음)로 본다.
        bool FestivalStale(GachaBanner banner)
        {
            return banner.DailyLimit && _store.GetInt(FestivalDayKey + banner.Id, 0) != DayStamp(DailyDay());
        }

        public int FestivalPulls(GachaBanner banner)
        {
            return FestivalStale(banner) ? 0 : Math.Max(0, _store.GetInt(FestivalPullsKey + banner.Id, 0));
        }

        public int FestivalRemaining(GachaBanner banner)
        {
            return Math.Max(0, banner.MaxPulls - FestivalPulls(banner));
        }

        public bool FestivalHadHigh(GachaBanner banner)
        {
            return !FestivalStale(banner) && _store.GetInt(FestivalHighKey + banner.Id, 0) != 0;
        }

        // 머신 천장 카운터(standard·pickup). daily·festival은 -1.
        public int Counter(GachaBanner banner)
        {
            return banner.Machine != null ? Inventory.Counter(banner.Machine.Id) : -1;
        }

        public bool TryPull(GachaBanner banner, int count, out GachaPullResult result)
        {
            result = null;
            if (Block(banner, count) != BannerBlock.None)
            {
                return false;
            }

            if (banner.Type == BannerType.Standard)
            {
                if (!TryPullMachine(banner.Machine, count, banner.Id, out result))
                {
                    return false;
                }

                result.Banner = banner;
                return true;
            }

            var before = _wallet.Balance;
            var cost = CostOf(banner, count);
            var snapshot = Inventory.ToJson();
            var keys = new BannerKeys(_store, banner);
            List<GearData> gears;
            GachaMachineData video;
            // 기록의 머신 id: 픽업은 배너 머신, 데일리·축제는 null(장비마다 그 장비의 머신).
            string history = null;
            switch (banner.Type)
            {
                case BannerType.Pickup:
                {
                    var counter = Inventory.Counter(banner.Machine.Id);
                    gears = _service.PullPickup(banner, count, ref counter);
                    Inventory.SetCounter(banner.Machine.Id, counter);
                    video = banner.Machine;
                    history = banner.Machine.Id;
                    break;
                }
                case BannerType.Daily:
                {
                    var machine = DailyMachine(banner);
                    var rates = new float[GachaService.RarityCount];
                    GachaService.Rates(banner, 0, false, rates, machine ?? Catalog.Machines[0]);
                    gears = _service.PullPool(machine != null ? machine.Gears : Catalog.AllGears, rates, count, banner.GuaranteeMin);
                    _store.SetInt(DailyDayKey + banner.Id, DayStamp(DailyDay()));
                    video = VideoMachineOf(Catalog, gears);
                    break;
                }
                case BannerType.Festival:
                {
                    var rates = new float[GachaService.RarityCount];
                    GachaService.Rates(banner, 0, false, rates);
                    var pulled = FestivalPulls(banner);
                    var hadHigh = FestivalHadHigh(banner);
                    if (FestivalStale(banner))
                    {
                        // 새 날: 지난 기록을 지우고 오늘 날짜로 시작한다(달러 차감이 실패하면 BannerKeys가 되돌린다).
                        _store.DeleteKey(FestivalHighKey + banner.Id);
                        _store.SetInt(FestivalDayKey + banner.Id, DayStamp(DailyDay()));
                    }

                    var pityIndex = !hadHigh && banner.PityAt > pulled && banner.PityAt <= pulled + count ? banner.PityAt - pulled - 1 : -1;
                    gears = _service.PullPool(Catalog.AllGears, rates, count, ItemRarity.Normal, pityIndex, banner.PityMin);
                    _store.SetInt(FestivalPullsKey + banner.Id, pulled + count);
                    if (!hadHigh && Highest(gears) >= banner.PityMin)
                    {
                        _store.SetInt(FestivalHighKey + banner.Id, 1);
                    }

                    video = VideoMachineOf(Catalog, gears);
                    break;
                }
                default:
                    return false;
            }

            var items = Mark(gears);
            Inventory.AddPulled(gears, history, _clock(), banner.Id);
            Inventory.Write(_store);

            if (cost > 0 && !_wallet.TrySpend(cost))
            {
                Inventory.RestoreJson(snapshot);
                Inventory.Write(_store);
                keys.Restore(_store);
                return false;
            }

            _store.Save();
            result = new GachaPullResult
            {
                Banner = banner,
                Machine = video,
                Count = count,
                Cost = cost,
                BalanceBefore = before,
                BalanceAfter = _wallet.Balance
            };
            result.Items.AddRange(items);
            return true;
        }

        // 최고 등급 장비(같은 등급이면 먼저 나온 것)의 머신.
        public static GachaMachineData VideoMachineOf(GachaCatalog catalog, IList<GearData> gears)
        {
            GearData best = null;
            foreach (var gear in gears)
            {
                if (gear != null && (best == null || gear.Rarity > best.Rarity))
                {
                    best = gear;
                }
            }

            return best != null ? catalog.MachineOf(best.Id) : null;
        }

        static ItemRarity Highest(IList<GearData> gears)
        {
            var best = ItemRarity.Normal;
            foreach (var gear in gears)
            {
                if (gear != null && gear.Rarity > best)
                {
                    best = gear.Rarity;
                }
            }

            return best;
        }

        List<PulledGear> Mark(IList<GearData> gears)
        {
            var pulled = new List<PulledGear>(gears.Count);
            var seen = new HashSet<string>();
            foreach (var gear in gears)
            {
                if (gear != null)
                {
                    pulled.Add(new PulledGear(gear, Inventory.Owned(gear.Id) > 0 || !seen.Add(gear.Id)));
                }
            }

            return pulled;
        }

        static int DayStamp(DateTime day)
        {
            return day.Year * 10000 + day.Month * 100 + day.Day;
        }

        // 달러 차감이 실패하면 배너 저장 키를 뽑기 전으로 되돌린다.
        readonly struct BannerKeys
        {
            readonly string _id;
            readonly int? _day;
            readonly int? _pulls;
            readonly int? _high;
            readonly int? _festivalDay;

            public BannerKeys(IKeyValueStore store, GachaBanner banner)
            {
                _id = banner.Id;
                _day = Read(store, DailyDayKey + _id);
                _pulls = Read(store, FestivalPullsKey + _id);
                _high = Read(store, FestivalHighKey + _id);
                _festivalDay = Read(store, FestivalDayKey + _id);
            }

            public void Restore(IKeyValueStore store)
            {
                Write(store, DailyDayKey + _id, _day);
                Write(store, FestivalPullsKey + _id, _pulls);
                Write(store, FestivalHighKey + _id, _high);
                Write(store, FestivalDayKey + _id, _festivalDay);
            }

            static int? Read(IKeyValueStore store, string key)
            {
                return store.HasKey(key) ? store.GetInt(key, 0) : (int?)null;
            }

            static void Write(IKeyValueStore store, string key, int? value)
            {
                if (value.HasValue)
                {
                    store.SetInt(key, value.Value);
                }
                else
                {
                    store.DeleteKey(key);
                }
            }
        }

        public int RefundValue(string gearId, int count = 1)
        {
            return Catalog.RefundValue(Catalog.FindGear(gearId)) * Math.Max(0, count);
        }

        public bool TryRefund(string gearId, GearState state, int count, out int dollars)
        {
            if (!Inventory.TryRemoveForRefund(gearId, state, count, out dollars))
            {
                return false;
            }

            Inventory.Write(_store);
            _wallet.Add(dollars);
            _store.Save();
            return true;
        }

        public bool Store(string gearId, int count)
        {
            return Commit(Inventory.Store(gearId, count));
        }

        public void StoreAllUnprocessed()
        {
            Inventory.StoreAllUnprocessed();
            Commit(true);
        }

        public bool Equip(string gearId)
        {
            return Commit(Inventory.Equip(gearId));
        }

        public bool Unequip(GearSlot slot)
        {
            return Commit(Inventory.Unequip(slot));
        }

        bool Commit(bool changed)
        {
            if (changed)
            {
                Inventory.Write(_store);
                _store.Save();
            }

            return changed;
        }
    }
}
