using System;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.Fishing;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    public static class SaveMigration
    {
        public const string VersionKey = "SaveVersion";
        public const int CurrentVersion = 4;

        // 반환값: 저장 데이터를 바꿨으면 true.
        // v2 → v3: 달러·강화 레벨을 그대로 두고 가챠 저장만 추가한다.
        // v3 → v4: 장비 데이터가 oripa_rewards.json(6머신 90종)으로 바뀌었다. 달러·강화·보유 장비·장착·천장 카운트는 유지하고,
        //          새 카탈로그에 없는 옛 장비 id만 등급 환원가만큼 달러로 돌려준다. 수트 머신 카운트는 헬멧 머신으로 옮긴다.
        // v2 미만: 예전 규칙대로 달러·강화를 초기화한다(밸런스가 바뀌기 전의 저장값).
        // 현재 버전 이상: 건드리지 않는다. 더 높은 버전을 낮은 빌드가 지워 버리는 일을 막는다.
        public static bool Run(IKeyValueStore store, GachaCatalog catalog = null)
        {
            var version = store.GetInt(VersionKey, 0);
            if (version >= CurrentVersion)
            {
                return false;
            }

            if (version < 2)
            {
                store.DeleteKey(Progression.DollarKey);
                store.DeleteKey(RodStats.LevelKey);
                foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
                {
                    store.DeleteKey(Progression.LevelKey(kind));
                }
            }

            EnsureGachaSave(store);
            RefundRetiredGear(store, catalog ?? GachaCatalog.LoadDefault());
            store.SetInt(VersionKey, CurrentVersion);
            store.Save();
            return true;
        }

        // 가챠 저장이 없거나 깨졌으면 빈 데이터로 새로 만든다. 실패해도 다른 키는 그대로 둔다.
        static void EnsureGachaSave(IKeyValueStore store)
        {
            try
            {
                var data = GearInventory.Read(store);
                store.SetString(GearInventory.SaveKey, JsonUtility.ToJson(data));
            }
            catch (Exception)
            {
                store.DeleteKey(GearInventory.SaveKey);
            }
        }

        // 반환값: 돌려준 달러.
        public static int RefundRetiredGear(IKeyValueStore store, GachaCatalog catalog)
        {
            if (catalog == null || catalog.Machines.Count == 0)
            {
                return 0;
            }

            var data = GearInventory.Read(store);
            var refund = 0;
            for (var i = data.gear.Count - 1; i >= 0; i--)
            {
                var stack = data.gear[i];
                if (catalog.FindGear(stack.id) != null)
                {
                    continue;
                }

                refund += (stack.unprocessed + stack.stored) * RetiredValue(stack.id, catalog);
                data.gear.RemoveAt(i);
            }

            for (var s = 0; s < data.equipped.Length; s++)
            {
                var id = data.equipped[s];
                if (string.IsNullOrEmpty(id) || catalog.FindGear(id) != null)
                {
                    continue;
                }

                refund += RetiredValue(id, catalog);
                data.equipped[s] = null;
            }

            foreach (var counter in data.counters)
            {
                if (counter != null && counter.machine == "machine_suit" && !data.counters.Exists(c => c != null && c.machine == "machine_helmet"))
                {
                    counter.machine = "machine_helmet";
                }
            }

            store.SetString(GearInventory.SaveKey, JsonUtility.ToJson(data));
            if (refund > 0)
            {
                store.SetInt(Progression.DollarKey, store.GetInt(Progression.DollarKey, 0) + refund);
                Debug.Log("옛 장비를 달러로 환원했습니다: +$" + refund);
            }

            return refund;
        }

        // 옛 장비 1개 값 = 같은 슬롯 머신의 1회 가격 × 그 등급의 환원 배수.
        public static int RetiredValue(string gearId, GachaCatalog catalog)
        {
            var rarity = RetiredRarity(gearId);
            var slot = RetiredSlot(gearId);
            var machine = slot.HasValue ? catalog.MachineForSlot(slot.Value) : null;
            var price = machine != null ? machine.Price : OripaCatalogJson.DefaultBasePrice;
            return Mathf.Max(0, Mathf.RoundToInt(price * catalog.RefundMultiplier(rarity)));
        }

        // 기획 데이터 id는 "슬롯_등급_번호"(suit_legend_01). 그 전의 코드 기본 장비는 GachaDefaults에서 등급을 찾는다.
        static ItemRarity RetiredRarity(string gearId)
        {
            var parts = (gearId ?? string.Empty).Split('_');
            if (parts.Length >= 3 && Enum.TryParse(parts[1], true, out ItemRarity parsed) && Enum.IsDefined(typeof(ItemRarity), parsed))
            {
                return parsed;
            }

            var defaults = GachaDefaults.CreateMachines();
            try
            {
                foreach (var machine in defaults)
                {
                    foreach (var gear in machine.Gears)
                    {
                        if (gear != null && gear.Id == gearId)
                        {
                            return gear.Rarity;
                        }
                    }
                }
            }
            finally
            {
                foreach (var machine in defaults)
                {
                    foreach (var gear in machine.Gears)
                    {
                        UnityEngine.Object.DestroyImmediate(gear);
                    }

                    UnityEngine.Object.DestroyImmediate(machine);
                }
            }

            return ItemRarity.Normal;
        }

        static GearSlot? RetiredSlot(string gearId)
        {
            var prefix = (gearId ?? string.Empty).Split('_')[0];
            if (prefix == "suit")
            {
                return GearSlot.Helmet;
            }

            return Enum.TryParse(prefix, true, out GearSlot slot) && Enum.IsDefined(typeof(GearSlot), slot) ? slot : (GearSlot?)null;
        }
    }
}
