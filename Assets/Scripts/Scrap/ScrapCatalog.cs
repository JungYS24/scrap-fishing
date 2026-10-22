using System.Collections.Generic;
using ScrapFishing.Core;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Scrap
{
    public static class ScrapCatalog
    {
        public const float ZoneMatchBonus = 3f;

        static readonly string[] ZoneNames = { "beach", "ocean", "abyss", "abyss" };
        static readonly ItemRarity[] RarityOrder =
        {
            ItemRarity.Normal,
            ItemRarity.Rare,
            ItemRarity.Epic,
            ItemRarity.Legend,
            ItemRarity.Synchro
        };
        static readonly int[] RarityCounts = new int[5];
        static readonly int[] RarityWeights = new int[5];
        static readonly List<ItemData> PickPool = new List<ItemData>(40);
        static float[] _itemWeights = new float[40];
        static ScrapDefinition[][] _zones;

        public static ScrapDefinition Pick(float depth)
        {
            var item = PickItem(depth);
            if (item != null)
            {
                return ScrapDefinition.FromItem(item);
            }

            Ensure();
            var table = _zones[DepthZone.Index(depth)];
            var roll = Random.value;
            if (roll < 0.52f) return table[0];
            if (roll < 0.86f) return table[1];
            return table[2];
        }

        // allowSynchro가 false면 싱크로 등급은 뽑지 않는다 (한 판에 1마리 제한).
        public static ItemData PickItem(float depth, bool allowSynchro = true)
        {
            var db = ItemDatabase.Instance;
            if (db == null || db.Items == null || db.Items.Count == 0)
            {
                return null;
            }

            var zoneIdx = DepthZone.Index(depth);
            var row = db.GetZoneRarity(zoneIdx);
            var items = db.Items;
            for (var i = 0; i < RarityCounts.Length; i++)
            {
                RarityCounts[i] = 0;
                RarityWeights[i] = 0;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                RarityCounts[RaritySlot(item.Rarity)]++;
            }

            var total = 0;
            total += SetRarityWeight(0, row.normal);
            total += SetRarityWeight(1, row.rare);
            total += SetRarityWeight(2, row.epic);
            total += SetRarityWeight(3, row.legend);
            total += SetRarityWeight(4, allowSynchro ? row.synchro : 0);
            if (total <= 0)
            {
                return PickUniform(items, allowSynchro);
            }

            var roll = Random.Range(0, total);
            var chosen = 0;
            var accumulated = 0;
            for (var i = 0; i < RarityWeights.Length; i++)
            {
                accumulated += RarityWeights[i];
                if (roll < accumulated)
                {
                    chosen = i;
                    break;
                }
            }

            var zoneName = ZoneNames[Mathf.Clamp(zoneIdx, 0, ZoneNames.Length - 1)];
            PickPool.Clear();
            var rarity = RarityOrder[chosen];
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.Rarity == rarity)
                {
                    PickPool.Add(item);
                }
            }

            if (PickPool.Count == 0)
            {
                return PickUniform(items, allowSynchro);
            }

            return PickWeighted(zoneName);
        }

        static int SetRarityWeight(int slot, int weight)
        {
            if (weight <= 0 || RarityCounts[slot] <= 0)
            {
                RarityWeights[slot] = 0;
                return 0;
            }

            RarityWeights[slot] = weight;
            return weight;
        }

        static int RaritySlot(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare:
                    return 1;
                case ItemRarity.Epic:
                    return 2;
                case ItemRarity.Legend:
                    return 3;
                case ItemRarity.Synchro:
                    return 4;
                default:
                    return 0;
            }
        }

        static ItemData PickUniform(IReadOnlyList<ItemData> items, bool allowSynchro)
        {
            var count = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (Pickable(items[i], allowSynchro))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return null;
            }

            var pick = Random.Range(0, count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!Pickable(item, allowSynchro))
                {
                    continue;
                }

                if (pick == 0)
                {
                    return item;
                }

                pick--;
            }

            return null;
        }

        static bool Pickable(ItemData item, bool allowSynchro)
        {
            return item != null && (allowSynchro || item.Rarity != ItemRarity.Synchro);
        }

        static ItemData PickWeighted(string zoneName)
        {
            var count = PickPool.Count;
            if (_itemWeights.Length < count)
            {
                var size = _itemWeights.Length;
                while (size < count)
                {
                    size *= 2;
                }

                _itemWeights = new float[size];
            }

            var total = 0f;
            for (var i = 0; i < count; i++)
            {
                var zone = PickPool[i].Zone;
                var weight = zone == zoneName ? ZoneMatchBonus : 1f;
                _itemWeights[i] = weight;
                total += weight;
            }

            var roll = Random.value * total;
            var accumulated = 0f;
            for (var i = 0; i < count; i++)
            {
                accumulated += _itemWeights[i];
                if (roll < accumulated)
                {
                    return PickPool[i];
                }
            }

            return PickPool[count - 1];
        }

        static void Ensure()
        {
            if (_zones != null)
            {
                return;
            }

            _zones = new[]
            {
                new[]
                {
                    ScrapDefinition.CreateRuntime("부유 합성수지", ScrapGrade.Junk, 8, new Color(0.62f, 0.62f, 0.55f)),
                    ScrapDefinition.CreateRuntime("깨진 네온 간판", ScrapGrade.Common, 18, Palette.Cyan),
                    ScrapDefinition.CreateRuntime("오염 피라미", ScrapGrade.Common, 22, new Color(0.45f, 0.85f, 0.4f))
                },
                new[]
                {
                    ScrapDefinition.CreateRuntime("드론 코어", ScrapGrade.Common, 28, Palette.Cyan),
                    ScrapDefinition.CreateRuntime("사이버 의수", ScrapGrade.Rare, 55, Palette.Magenta),
                    ScrapDefinition.CreateRuntime("네온 뱀장어", ScrapGrade.Rare, 62, Palette.NeonGreen)
                },
                new[]
                {
                    ScrapDefinition.CreateRuntime("화학 드럼", ScrapGrade.Rare, 70, new Color(0.85f, 0.55f, 0.15f)),
                    ScrapDefinition.CreateRuntime("데이터 칩", ScrapGrade.Rare, 80, Palette.Magenta),
                    ScrapDefinition.CreateRuntime("기계 아귀", ScrapGrade.Mutant, 95, Palette.NeonGreen)
                },
                new[]
                {
                    ScrapDefinition.CreateRuntime("AI 코어", ScrapGrade.Mutant, 110, Palette.Cyan),
                    ScrapDefinition.CreateRuntime("메카 잔해", ScrapGrade.Mutant, 125, Palette.Magenta),
                    ScrapDefinition.CreateRuntime("변이 크라켄", ScrapGrade.Mutant, 150, Palette.NeonGreen)
                }
            };
        }
    }
}
