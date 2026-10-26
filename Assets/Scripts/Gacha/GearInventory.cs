using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    [Serializable]
    public class GearStack
    {
        public string id;
        public int unprocessed;
        public int stored;
    }

    [Serializable]
    public class GachaCounter
    {
        public string machine;
        public int count;
    }

    [Serializable]
    public class GachaRecord
    {
        // 장비가 속한 머신 id(데일리·축제도 장비의 머신). banner는 뽑은 배너 id이며, 배너 이전 세이브에는 없다(null).
        public string machine;
        public string banner;
        public string gear;
        public int rarity;
        public long time;
    }

    // 장비는 개체마다 저장하지 않고 id별 개수로 묶는다. 장착품은 슬롯마다 1개이고 개수에 포함하지 않는다.
    [Serializable]
    public class GachaSaveData
    {
        public int version = 1;
        public List<GearStack> gear = new List<GearStack>();
        public string[] equipped = new string[GearInventory.SlotCount];
        public List<GachaCounter> counters = new List<GachaCounter>();
        public List<GachaRecord> history = new List<GachaRecord>();
    }

    public readonly struct GearEntry
    {
        public readonly GearData Gear;
        public readonly int Count;
        public readonly GearState State;

        public GearEntry(GearData gear, int count, GearState state)
        {
            Gear = gear;
            Count = count;
            State = state;
        }
    }

    public sealed class GearInventory
    {
        public const string SaveKey = "GachaSave";
        public const int HistoryLimit = 50;
        public const int SlotCount = 6;

        readonly GachaCatalog _catalog;
        GachaSaveData _data;

        public IReadOnlyList<GachaRecord> History => _data.history;

        public GearInventory(GachaCatalog catalog, GachaSaveData data = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _data = Sanitize(data);
        }

        public static GearInventory Load(IKeyValueStore store, GachaCatalog catalog)
        {
            return new GearInventory(catalog, Read(store));
        }

        // 저장값이 없거나 깨졌으면 빈 데이터를 돌려준다. 달러·강화 키는 건드리지 않는다.
        public static GachaSaveData Read(IKeyValueStore store)
        {
            var json = store != null ? store.GetString(SaveKey, null) : null;
            if (string.IsNullOrEmpty(json))
            {
                return new GachaSaveData();
            }

            try
            {
                return Sanitize(JsonUtility.FromJson<GachaSaveData>(json));
            }
            catch (Exception)
            {
                return new GachaSaveData();
            }
        }

        public void Write(IKeyValueStore store)
        {
            store.SetString(SaveKey, ToJson());
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(_data);
        }

        public void RestoreJson(string json)
        {
            GachaSaveData data = null;
            try
            {
                data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<GachaSaveData>(json);
            }
            catch (Exception)
            {
                data = null;
            }

            _data = Sanitize(data);
        }

        public int Count(string gearId, GearState state)
        {
            if (state == GearState.Equipped)
            {
                var gear = _catalog.FindGear(gearId);
                return gear != null && EquippedId(gear.Slot) == gearId ? 1 : 0;
            }

            var stack = Find(gearId);
            if (stack == null)
            {
                return 0;
            }

            return state == GearState.Unprocessed ? stack.unprocessed : stack.stored;
        }

        public int Owned(string gearId)
        {
            return Count(gearId, GearState.Unprocessed) + Count(gearId, GearState.Stored) + Count(gearId, GearState.Equipped);
        }

        public string EquippedId(GearSlot slot)
        {
            var id = _data.equipped[(int)slot];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        public GearData Equipped(GearSlot slot)
        {
            return _catalog.FindGear(EquippedId(slot));
        }

        // 보관함 탭(상태)과 카테고리(슬롯, null이면 전체)로 거른 목록. 높은 등급이 먼저 온다.
        public List<GearEntry> List(GearState state, GearSlot? slot = null)
        {
            var list = new List<GearEntry>();
            if (state == GearState.Equipped)
            {
                for (var s = 0; s < SlotCount; s++)
                {
                    var gear = Equipped((GearSlot)s);
                    if (gear != null && (slot == null || gear.Slot == slot.Value))
                    {
                        list.Add(new GearEntry(gear, 1, state));
                    }
                }

                return list;
            }

            for (var i = 0; i < _data.gear.Count; i++)
            {
                var stack = _data.gear[i];
                var count = state == GearState.Unprocessed ? stack.unprocessed : stack.stored;
                var gear = _catalog.FindGear(stack.id);
                if (count <= 0 || gear == null || (slot != null && gear.Slot != slot.Value))
                {
                    continue;
                }

                list.Add(new GearEntry(gear, count, state));
            }

            list.Sort((a, b) =>
            {
                var byRarity = b.Gear.Rarity.CompareTo(a.Gear.Rarity);
                return byRarity != 0 ? byRarity : string.CompareOrdinal(a.Gear.Id, b.Gear.Id);
            });
            return list;
        }

        public int Counter(string machineId)
        {
            for (var i = 0; i < _data.counters.Count; i++)
            {
                if (_data.counters[i].machine == machineId)
                {
                    return Math.Max(0, _data.counters[i].count);
                }
            }

            return 0;
        }

        public void SetCounter(string machineId, int count)
        {
            for (var i = 0; i < _data.counters.Count; i++)
            {
                if (_data.counters[i].machine == machineId)
                {
                    _data.counters[i].count = Math.Max(0, count);
                    return;
                }
            }

            _data.counters.Add(new GachaCounter { machine = machineId, count = Math.Max(0, count) });
        }

        // machineId가 null이면 장비마다 카탈로그의 머신 id를 기록한다(여러 머신이 섞이는 데일리·축제).
        public void AddPulled(IList<GearData> gears, string machineId, long time, string bannerId = null)
        {
            for (var i = 0; i < gears.Count; i++)
            {
                var gear = gears[i];
                if (gear == null)
                {
                    continue;
                }

                GetOrAdd(gear.Id).unprocessed++;
                var machine = machineId;
                if (machine == null)
                {
                    var owner = _catalog.MachineOf(gear.Id);
                    machine = owner != null ? owner.Id : null;
                }

                _data.history.Add(new GachaRecord { machine = machine, banner = bannerId, gear = gear.Id, rarity = (int)gear.Rarity, time = time });
            }

            var overflow = _data.history.Count - HistoryLimit;
            if (overflow > 0)
            {
                _data.history.RemoveRange(0, overflow);
            }
        }

        public bool Store(string gearId, int count)
        {
            var stack = Find(gearId);
            if (stack == null || count <= 0 || stack.unprocessed < count)
            {
                return false;
            }

            stack.unprocessed -= count;
            stack.stored += count;
            return true;
        }

        public void StoreAllUnprocessed()
        {
            for (var i = 0; i < _data.gear.Count; i++)
            {
                _data.gear[i].stored += _data.gear[i].unprocessed;
                _data.gear[i].unprocessed = 0;
            }
        }

        // 장착 칸이 빈 슬롯마다 {슬롯}_normal_01을 장착한다. 이미 갖고 있으면 그것을 쓰고, 없을 때만 1개를 넣는다(중복 없음).
        // 이미 장착한 장비와 보관함의 다른 아이템은 건드리지 않는다. 반환값: 바뀐 것이 있으면 true.
        public bool EnsureStarterGear()
        {
            var changed = false;
            for (var s = 0; s < SlotCount; s++)
            {
                var slot = (GearSlot)s;
                if (EquippedId(slot) != null)
                {
                    continue;
                }

                var id = GearVisuals.DefaultId(slot);
                if (_catalog.FindGear(id) == null)
                {
                    continue;
                }

                if (Count(id, GearState.Stored) + Count(id, GearState.Unprocessed) == 0)
                {
                    GetOrAdd(id).stored++;
                }

                changed |= Equip(id);
            }

            return changed;
        }

        // 보관 중 → 미처리 순으로 1개를 꺼내 장착한다. 기존 장착품은 보관 중으로 돌아간다.
        public bool Equip(string gearId)
        {
            var gear = _catalog.FindGear(gearId);
            var stack = Find(gearId);
            if (gear == null || stack == null || EquippedId(gear.Slot) == gearId)
            {
                return false;
            }

            if (stack.stored > 0)
            {
                stack.stored--;
            }
            else if (stack.unprocessed > 0)
            {
                stack.unprocessed--;
            }
            else
            {
                return false;
            }

            Unequip(gear.Slot);
            _data.equipped[(int)gear.Slot] = gearId;
            return true;
        }

        public bool Unequip(GearSlot slot)
        {
            var current = EquippedId(slot);
            if (current == null)
            {
                return false;
            }

            GetOrAdd(current).stored++;
            _data.equipped[(int)slot] = null;
            return true;
        }

        // 장착 중인 장비는 환원할 수 없다. 성공하면 받을 달러를 돌려준다.
        public bool TryRemoveForRefund(string gearId, GearState state, int count, out int dollars)
        {
            dollars = 0;
            var gear = _catalog.FindGear(gearId);
            var stack = Find(gearId);
            if (gear == null || stack == null || count <= 0 || state == GearState.Equipped)
            {
                return false;
            }

            if (state == GearState.Unprocessed)
            {
                if (stack.unprocessed < count)
                {
                    return false;
                }

                stack.unprocessed -= count;
            }
            else
            {
                if (stack.stored < count)
                {
                    return false;
                }

                stack.stored -= count;
            }

            dollars = _catalog.RefundValue(gear) * count;
            return true;
        }

        GearStack Find(string gearId)
        {
            if (string.IsNullOrEmpty(gearId))
            {
                return null;
            }

            for (var i = 0; i < _data.gear.Count; i++)
            {
                if (_data.gear[i].id == gearId)
                {
                    return _data.gear[i];
                }
            }

            return null;
        }

        GearStack GetOrAdd(string gearId)
        {
            var stack = Find(gearId);
            if (stack != null)
            {
                return stack;
            }

            stack = new GearStack { id = gearId };
            _data.gear.Add(stack);
            return stack;
        }

        static GachaSaveData Sanitize(GachaSaveData data)
        {
            data = data ?? new GachaSaveData();
            data.gear = data.gear ?? new List<GearStack>();
            data.gear.RemoveAll(stack => stack == null || string.IsNullOrEmpty(stack.id));
            for (var i = 0; i < data.gear.Count; i++)
            {
                data.gear[i].unprocessed = Math.Max(0, data.gear[i].unprocessed);
                data.gear[i].stored = Math.Max(0, data.gear[i].stored);
            }

            if (data.equipped == null || data.equipped.Length != SlotCount)
            {
                var equipped = new string[SlotCount];
                if (data.equipped != null)
                {
                    Array.Copy(data.equipped, equipped, Math.Min(SlotCount, data.equipped.Length));
                }

                data.equipped = equipped;
            }

            data.counters = data.counters ?? new List<GachaCounter>();
            data.history = data.history ?? new List<GachaRecord>();
            if (data.history.Count > HistoryLimit)
            {
                data.history.RemoveRange(0, data.history.Count - HistoryLimit);
            }

            return data;
        }
    }
}
