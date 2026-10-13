using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Data
{
    [CreateAssetMenu(menuName = "ScrapFishing/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] List<ItemData> items = new List<ItemData>();

        static ItemDatabase _instance;

        public IReadOnlyList<ItemData> Items => items;

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
    }
}
