using System.Collections.Generic;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.UI;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    public static class ResultsPreviewMenu
    {
        [MenuItem("Scrap Fishing/Preview Results/0 CY")]
        static void Zero()
        {
            Show(0, 0, false, null, 0);
        }

        [MenuItem("Scrap Fishing/Preview Results/평범한 판")]
        static void Normal()
        {
            var items = new List<ItemData>();
            items.AddRange(Find(ItemRarity.Normal, 2));
            if (items.Count > 0)
            {
                items.Add(items[0]);
                items.Add(items[0]);
            }

            items.AddRange(Find(ItemRarity.Rare, 2));
            Show(860, 40, false, items.ToArray(), 2);
        }

        [MenuItem("Scrap Fishing/Preview Results/레전드 + 신기록")]
        static void Legend()
        {
            var items = new List<ItemData>();
            items.AddRange(Find(ItemRarity.Legend, 1));
            items.AddRange(Find(ItemRarity.Epic, 2));
            items.AddRange(Find(ItemRarity.Rare, 2));
            items.AddRange(Find(ItemRarity.Normal, 1));
            Show(3200, 45, true, items.ToArray(), 0);
        }

        static void Show(int cy, int gold, bool newBest, ItemData[] items, int hazards)
        {
            if (!Application.isPlaying)
            {
                Debug.Log("Play 모드에서 Scrap Fishing/Preview Results 를 실행하세요.");
                return;
            }

            var session = Object.FindFirstObjectByType<RunSession>();
            var results = Object.FindFirstObjectByType<ResultsView>();
            if (session == null || results == null)
            {
                Debug.LogError("실행 중인 보트 씬에서 RunSession과 ResultsView를 찾지 못했습니다.");
                return;
            }

            session.PreviewResults(cy, gold, newBest, items, hazards);
            results.Show(session);
        }

        static List<ItemData> Find(ItemRarity rarity, int count)
        {
            var found = new List<ItemData>();
            var guids = AssetDatabase.FindAssets("t:ItemData");
            for (var i = 0; i < guids.Length && found.Count < count; i++)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (item != null && item.Rarity == rarity)
                {
                    found.Add(item);
                }
            }

            return found;
        }
    }
}
