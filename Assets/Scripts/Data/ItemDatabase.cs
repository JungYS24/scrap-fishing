using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Data
{
    [System.Serializable]
    public struct ZoneRarityWeights
    {
        public int index;
        public string name;
        public int normal;
        public int rare;
        public int epic;
        public int legend;
    }

    [CreateAssetMenu(menuName = "ScrapFishing/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] List<ItemData> items = new List<ItemData>();
        [SerializeField] List<ZoneRarityWeights> zoneRarity = new List<ZoneRarityWeights>();

        static readonly List<ZoneRarityWeights> EmptyZoneRarity = new List<ZoneRarityWeights>();

        static ItemDatabase _instance;

        public IReadOnlyList<ItemData> Items => items;
        public IReadOnlyList<ZoneRarityWeights> ZoneRarity => zoneRarity ?? EmptyZoneRarity;

        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ItemDatabase>("ItemDatabase");
                }

                return _instance;
            }
        }

#if UNITY_EDITOR
        public static void BindEditorInstance(ItemDatabase database)
        {
            _instance = database;
        }
#endif

        public ItemData GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || items == null)
            {
                return null;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.Id == id)
                {
                    return item;
                }
            }

            return null;
        }

        public List<ItemData> GetByZone(string zone)
        {
            var result = new List<ItemData>();
            if (items == null)
            {
                return result;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.Zone == zone)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public ZoneRarityWeights GetZoneRarity(int index)
        {
            if (zoneRarity == null || zoneRarity.Count == 0)
            {
                return default;
            }

            var best = 0;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < zoneRarity.Count; i++)
            {
                var distance = Mathf.Abs(zoneRarity[i].index - index);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return zoneRarity[best];
        }

        public List<ItemData> GetByRarity(ItemRarity rarity)
        {
            var result = new List<ItemData>();
            if (items == null)
            {
                return result;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.Rarity == rarity)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public List<ItemData> GetByCategory(ItemCategory category)
        {
            var result = new List<ItemData>();
            if (items == null)
            {
                return result;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.Category == category)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public ItemData GetRandom(string zone)
        {
            var pool = string.IsNullOrEmpty(zone) ? items : GetByZone(zone);
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }

        public void ReplaceItems(List<ItemData> next)
        {
            if (items == null)
            {
                items = new List<ItemData>();
            }

            items.Clear();
            if (next == null)
            {
                return;
            }

            items.AddRange(next);
        }

        public void ReplaceZoneRarity(List<ZoneRarityWeights> next)
        {
            if (zoneRarity == null)
            {
                zoneRarity = new List<ZoneRarityWeights>();
            }

            zoneRarity.Clear();
            if (next == null)
            {
                return;
            }

            zoneRarity.AddRange(next);
        }
    }
}
