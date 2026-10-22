using System;
using System.Collections.Generic;
using ScrapFishing.Core;

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
        public GachaMachineData Machine;
        public int Count;
        public int Cost;
        public int BalanceBefore;
        public int BalanceAfter;
        public readonly List<PulledGear> Items = new List<PulledGear>();
    }

    // 확인 버튼을 누른 순간 결과를 계산해 달러 차감과 함께 저장한다. 연출은 그 뒤에 시작한다.
    public sealed class GachaShop
    {
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

        public static GachaShop CreateDefault()
        {
            return new GachaShop(GachaCatalog.LoadDefault(), new GachaService(new SystemGachaRandom()), ProgressionWallet.Instance, PlayerPrefsStore.Instance);
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
            Inventory.AddPulled(gears, machine.Id, _clock());
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
