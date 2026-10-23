using System;
using System.Collections.Generic;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // Resources/Gacha/oripa_rewards.json(기획 데이터)을 머신·장비로 바꾼다.
    // 1회 가격 = basePrice × pricePerPull. basePrice가 0(미확정)이면 DefaultBasePrice를 쓴다.
    public static class OripaCatalogJson
    {
        public const string Resource = "Gacha/oripa_rewards";
        public const int DefaultBasePrice = 5000;
        const int BoostStart = 51;
        const float BoostLegendRate = 5f;

        public static GachaMachineData[] Load()
        {
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null)
            {
                return null;
            }

            try
            {
                return Parse(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError("oripa_rewards.json을 읽지 못했습니다: " + e.Message);
                return null;
            }
        }

        public static GachaMachineData[] Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new FormatException("최상위가 객체가 아닙니다.");
            }

            var basePrice = Number(root, "basePrice");
            var unit = basePrice > 0 ? basePrice : DefaultBasePrice;
            var rates = new float[GachaService.RarityCount];
            var refunds = new float[GachaService.RarityCount];
            foreach (var entry in List(root, "grades"))
            {
                var grade = entry as Dictionary<string, object>;
                if (grade == null || !TryRarity(Text(grade, "id"), out var rarity))
                {
                    continue;
                }

                rates[(int)rarity] = (float)(Number(grade, "dropRate") * 100.0);
                refunds[(int)rarity] = (float)Number(grade, "refundMultiplier");
            }

            var machines = new List<GachaMachineData>();
            foreach (var entry in List(root, "machines"))
            {
                var machine = entry as Dictionary<string, object>;
                if (machine == null)
                {
                    continue;
                }

                if (!Enum.TryParse(Text(machine, "slot"), out GearSlot slot))
                {
                    Debug.LogWarning("알 수 없는 슬롯: " + Text(machine, "slot"));
                    continue;
                }

                var gears = new List<GearData>();
                if (machine.TryGetValue("items", out var itemsValue) && itemsValue is Dictionary<string, object> byGrade)
                {
                    foreach (var pair in byGrade)
                    {
                        if (!TryRarity(pair.Key, out var rarity) || !(pair.Value is List<object> list))
                        {
                            continue;
                        }

                        foreach (var itemValue in list)
                        {
                            if (itemValue is Dictionary<string, object> item)
                            {
                                gears.Add(Gear(item, slot, rarity, refunds[(int)rarity]));
                            }
                        }
                    }
                }

                var data = ScriptableObject.CreateInstance<GachaMachineData>();
                var price = Mathf.Max(1, Mathf.RoundToInt((float)(unit * Number(machine, "pricePerPull", 1.0))));
                data.Configure("machine_" + Text(machine, "id"), Text(machine, "nameKo"), slot, price, gears);
                data.SetRates(rates[0], rates[1], rates[2], rates[3], rates[4]);
                var pity = (int)Number(machine, "pityLegendAt", 100.0);
                data.SetBoost(pity + 1, BoostStart, BoostLegendRate);
                data.SetCopy(Text(machine, "nameEn"), Text(machine, "bannerCopy"));
                machines.Add(data);
            }

            return machines.ToArray();
        }

        static GearData Gear(Dictionary<string, object> item, GearSlot slot, ItemRarity rarity, float refund)
        {
            var effects = new List<GearEffect>();
            if (item.TryGetValue("effects", out var effectsValue) && effectsValue is Dictionary<string, object> map)
            {
                foreach (var pair in map)
                {
                    var value = pair.Value is double number ? (float)number : 0f;
                    if (Mathf.Approximately(value, 0f))
                    {
                        continue;
                    }

                    if (TryStat(pair.Key, slot, out var stat))
                    {
                        effects.Add(new GearEffect(stat, value));
                    }
                    else
                    {
                        Debug.LogWarning("알 수 없는 효과 키: " + pair.Key + " (" + Text(item, "id") + ")");
                    }
                }
            }

            var gear = GearData.Create(Text(item, "id"), Text(item, "nameKo"), slot, rarity, effects.ToArray());
            var spritePath = Text(item, "sprite");
            gear.SetDetails(Text(item, "desc"), refund, string.IsNullOrEmpty(spritePath) ? null : Resources.Load<Sprite>(spritePath));
            return gear;
        }

        // magnetRadiusPct는 루어에서는 캐스팅 흡착 반경, 드론에서는 잠수 흡착 반경이다.
        public static bool TryStat(string key, GearSlot slot, out GearStat stat)
        {
            switch (key)
            {
                case "lureCapacity": stat = GearStat.LureCapacity; return true;
                case "magnetRadiusPct": stat = slot == GearSlot.Drone ? GearStat.DroneRadiusPercent : GearStat.CatchRadiusPercent; return true;
                case "rareChancePct": stat = GearStat.RareChancePercent; return true;
                case "spawnCapBonus": stat = GearStat.SpawnCapBonus; return true;
                case "sellPricePct": stat = GearStat.SellPercent; return true;
                case "gaugeSpeedPct": stat = GearStat.GaugeSpeedPercent; return true;
                case "runBonusPct": stat = GearStat.EndBonusPercent; return true;
                case "autoCollectSec": stat = GearStat.AutoCollectSeconds; return true;
                case "hazardWarning": stat = GearStat.HazardWarning; return true;
                case "hazardBlock": stat = GearStat.HazardShield; return true;
                case "oxygenPct": stat = GearStat.OxygenPercent; return true;
                case "hazardDamageReductionPct": stat = GearStat.HazardDamageReductionPercent; return true;
                case "steerSpeedPct": stat = GearStat.SteerSpeedPercent; return true;
                case "reelSpeedPct": stat = GearStat.ReelSpeedPercent; return true;
                case "diveSpeedPct": stat = GearStat.DiveSpeedPercent; return true;
                case "diveEveryCastsBonus": stat = GearStat.DiveEveryCastsBonus; return true;
                default: stat = default; return false;
            }
        }

        static bool TryRarity(string id, out ItemRarity rarity)
        {
            return Enum.TryParse(id, out rarity) && Enum.IsDefined(typeof(ItemRarity), rarity);
        }

        static IEnumerable<object> List(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out var value) && value is List<object> list ? list : (IEnumerable<object>)Array.Empty<object>();
        }

        static string Text(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out var value) && value is string text ? text : string.Empty;
        }

        static double Number(Dictionary<string, object> map, string key, double fallback = 0.0)
        {
            return map.TryGetValue(key, out var value) && value is double number ? number : fallback;
        }
    }
}
