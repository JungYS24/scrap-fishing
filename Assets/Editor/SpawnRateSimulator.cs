using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.Scrap;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    public static class SpawnRateSimulator
    {
        const int Samples = 10000;
        const float TolerancePoints = 2f;
        const string DatabasePath = "Assets/Resources/ItemDatabase.asset";

        static readonly float[] ZoneDepths = { 0.1f, 0.35f, 0.6f, 0.9f };
        static readonly string[] RarityLabels = { "normal", "rare", "epic", "legend", "synchro" };

        [MenuItem("Tools/Scrap Fishing/Simulate Spawn Rates")]
        public static void SimulateSpawnRates()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (db == null || db.Items == null || db.Items.Count == 0)
            {
                Debug.LogError("ItemDatabase가 비어 있어 스폰 확률을 시뮬레이션할 수 없습니다.");
                return;
            }

            ItemDatabase.BindEditorInstance(db);

            var allPass = true;
            var report = "Simulate Spawn Rates (" + Samples + "회)\n";
            for (var zone = 0; zone < ZoneDepths.Length; zone++)
            {
                if (DepthZone.Index(ZoneDepths[zone]) != zone)
                {
                    Debug.LogError("시뮬레이션 깊이 매핑 오류: zone " + zone);
                    return;
                }

                var row = db.GetZoneRarity(zone);
                var expected = new[] { row.normal, row.rare, row.epic, row.legend, row.synchro };
                var sum = expected[0] + expected[1] + expected[2] + expected[3] + expected[4];
                var counts = new int[expected.Length];
                for (var i = 0; i < Samples; i++)
                {
                    var item = ScrapCatalog.PickItem(ZoneDepths[zone]);
                    if (item == null)
                    {
                        continue;
                    }

                    counts[RaritySlot(item.Rarity)]++;
                }

                var zonePass = sum > 0;
                report += "[" + zone + "] " + row.name + "\n";
                for (var r = 0; r < expected.Length; r++)
                {
                    var expectedPct = sum > 0 ? expected[r] * 100f / sum : 0f;
                    var actualPct = counts[r] * 100f / Samples;
                    var delta = Mathf.Abs(actualPct - expectedPct);
                    var pass = sum > 0 && delta <= TolerancePoints;
                    if (!pass)
                    {
                        zonePass = false;
                    }

                    report += "  " + RarityLabels[r] +
                              "  expected " + expectedPct.ToString("0.00") + "%" +
                              "  actual " + actualPct.ToString("0.00") + "%" +
                              "  delta " + delta.ToString("0.00") +
                              "  " + (pass ? "PASS" : "FAIL") + "\n";
                }

                report += zonePass ? "  ZONE PASS\n" : "  ZONE FAIL\n";
                if (!zonePass)
                {
                    allPass = false;
                }
            }

            report += allPass ? "RESULT PASS" : "RESULT FAIL";
            if (allPass)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
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
    }
}
