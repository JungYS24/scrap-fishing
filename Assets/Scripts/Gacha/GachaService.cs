using System;
using System.Collections.Generic;
using ScrapFishing.Data;

namespace ScrapFishing.Gacha
{
    // 뽑기 계산만 담당한다. 저장·달러·UI는 모른다.
    public sealed class GachaService
    {
        public const int TenPull = 10;
        public const int RarityCount = 5;

        readonly IGachaRandom _random;
        readonly float[] _weights = new float[RarityCount];

        public GachaService(IGachaRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        // 10연은 Rare 이상 1개를 보장한다. 그 외(1회)는 보장 없음.
        public static ItemRarity GuaranteeFor(int count)
        {
            return count >= TenPull ? ItemRarity.Rare : ItemRarity.Normal;
        }

        // counter: 이 머신에서 Legend(또는 Synchro) 없이 뽑은 횟수(주기 안). 이번 뽑기는 counter+1 번째다.
        public static float LegendRate(GachaMachineData machine, int counter, bool boost = true)
        {
            return LegendRate(machine, Normalized(machine, ItemRarity.Legend), counter, boost);
        }

        // baseRate에서 시작해 머신의 보간 방식(시작 지점·주기·최고 확률)대로 올린다.
        static float LegendRate(GachaMachineData machine, float baseRate, int counter, bool boost)
        {
            var cycle = Math.Max(1, machine.BoostCycle);
            var start = Math.Max(1, machine.BoostStart);
            var index = counter + 1;
            if (!boost || index < start || cycle <= start)
            {
                return baseRate;
            }

            var t = Math.Min(1f, (index - start) / (float)(cycle - start));
            var peak = Math.Max(baseRate, machine.BoostLegendRate / 100f);
            return baseRate + (peak - baseRate) * t;
        }

        public static int NextCounter(GachaMachineData machine, int counter, ItemRarity rolled)
        {
            if (rolled >= ItemRarity.Legend)
            {
                return 0;
            }

            var next = counter + 1;
            return next >= Math.Max(1, machine.BoostCycle) ? 0 : next;
        }

        // Legend 확률이 올라가면 나머지 등급은 기본 비율을 유지한 채 줄어든다. Synchro는 보간 없이 기본 확률 그대로다.
        public static void Rates(GachaMachineData machine, int counter, bool boost, float[] into)
        {
            Rates(machine, Normalized(machine, ItemRarity.Legend), Normalized(machine, ItemRarity.Synchro), counter, boost, into);
        }

        // 레전드·싱크로 기본 확률만 바꾼 머신 확률(싱크로 픽업). 노멀·레어·에픽은 머신 비율대로 나머지를 나눈다.
        public static void Rates(GachaMachineData machine, float legendBase, float synchroBase, int counter, bool boost, float[] into)
        {
            var legend = LegendRate(machine, legendBase, counter, boost);
            var synchro = synchroBase;
            var normal = Math.Max(0f, machine.BaseRate(ItemRarity.Normal));
            var rare = Math.Max(0f, machine.BaseRate(ItemRarity.Rare));
            var epic = Math.Max(0f, machine.BaseRate(ItemRarity.Epic));
            var rest = normal + rare + epic;
            var scale = rest > 0f ? Math.Max(0f, 1f - legend - synchro) / rest : 0f;
            into[0] = normal * scale;
            into[1] = rare * scale;
            into[2] = epic * scale;
            into[3] = legend;
            into[4] = synchro;
        }

        // 보간을 포함한 장기 평균 등급 확률. 주기 안 카운트 상태의 정상 분포로 정확히 계산한다.
        public static float[] EffectiveRates(GachaMachineData machine)
        {
            return EffectiveRates(machine, Normalized(machine, ItemRarity.Legend), Normalized(machine, ItemRarity.Synchro));
        }

        public static float[] EffectiveRates(GachaMachineData machine, float legendBase, float synchroBase)
        {
            var cycle = Math.Max(1, machine.BoostCycle);
            var rates = new float[RarityCount];
            var step = new float[RarityCount];
            var survive = 1.0;
            var total = 0.0;
            var sums = new double[RarityCount];
            for (var counter = 0; counter < cycle; counter++)
            {
                Rates(machine, legendBase, synchroBase, counter, true, step);
                for (var r = 0; r < RarityCount; r++)
                {
                    sums[r] += survive * step[r];
                }

                total += survive;
                survive *= 1.0 - step[3] - step[4];
            }

            for (var r = 0; r < RarityCount; r++)
            {
                rates[r] = total > 0.0 ? (float)(sums[r] / total) : 0f;
            }

            return rates;
        }

        // 1회 가격 대비 환원 기댓값(보간 포함, 10연 보장 제외).
        public static float ExpectedRefundRate(GachaMachineData machine)
        {
            var rates = EffectiveRates(machine);
            var expected = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                expected += rates[r] * AverageRefundMultiplier(machine, (ItemRarity)r);
            }

            return expected;
        }

        // 배너의 이번 뽑기 등급 확률. standard·레전드 픽업은 머신 그대로, 싱크로 픽업은 레전드·싱크로 기본값만 바꾼다.
        // festival은 배너 확률, daily는 오늘 풀 머신(dailyMachine)의 기본 확률이다(둘 다 머신 카운터·보간 없음).
        public static void Rates(GachaBanner banner, int counter, bool boost, float[] into, GachaMachineData dailyMachine = null)
        {
            switch (banner.Type)
            {
                case BannerType.Festival:
                    FixedRates(banner.GradeRates, into);
                    return;
                case BannerType.Daily:
                    Rates(dailyMachine, 0, false, into);
                    return;
                case BannerType.Pickup when IsSynchroPickup(banner):
                    Rates(banner.Machine, banner.LegendRate, banner.SynchroRate, counter, boost, into);
                    return;
                default:
                    Rates(banner.Machine, counter, boost, into);
                    return;
            }
        }

        // 이번 뽑기에서 픽업 장비가 나올 확률. 픽업이 아니면 0.
        public static float FeaturedRate(GachaBanner banner, int counter, bool boost = true)
        {
            if (banner == null || banner.Type != BannerType.Pickup || banner.Featured == null)
            {
                return 0f;
            }

            var rates = new float[RarityCount];
            Rates(banner, counter, boost, rates);
            var rarity = banner.Featured.Rarity;
            return rates[(int)rarity] * FeaturedShareOf(banner);
        }

        // 픽업 등급이 나왔을 때 픽업 장비가 될 몫. 레전드 픽업은 배너 값, 싱크로 픽업은 그 등급 안에서 균등.
        public static float FeaturedShareOf(GachaBanner banner)
        {
            if (banner.Pickup == PickupKind.Legend)
            {
                return banner.FeaturedShare;
            }

            var same = 0;
            foreach (var gear in banner.Machine.Gears)
            {
                if (gear != null && gear.Rarity == banner.Featured.Rarity)
                {
                    same++;
                }
            }

            return same > 0 ? 1f / same : 0f;
        }

        // 배너 1회 가격 대비 환원 기댓값(보간 포함, 10연 보장 제외). 환원가는 장비 머신의 1회 가격 기준이라
        // 배너 가격과 다를 수 있다(싱크로 픽업). 무료(가격 0)면 0.
        public static float ExpectedRefundRate(GachaBanner banner, GachaCatalog catalog)
        {
            if (banner == null || catalog == null || banner.Price <= 0)
            {
                return 0f;
            }

            float[] rates;
            IReadOnlyList<GearData> pool;
            switch (banner.Type)
            {
                case BannerType.Festival:
                    rates = new float[RarityCount];
                    FixedRates(banner.GradeRates, rates);
                    pool = catalog.AllGears;
                    break;
                case BannerType.Pickup when IsSynchroPickup(banner):
                    rates = EffectiveRates(banner.Machine, banner.LegendRate, banner.SynchroRate);
                    pool = banner.Machine.Gears;
                    break;
                case BannerType.Standard:
                case BannerType.Pickup:
                    rates = EffectiveRates(banner.Machine);
                    pool = banner.Machine.Gears;
                    break;
                default:
                    return 0f;
            }

            var expected = 0.0;
            for (var r = 0; r < RarityCount; r++)
            {
                expected += rates[r] * AverageRefund(catalog, pool, (ItemRarity)r);
            }

            return (float)(expected / banner.Price);
        }

        static bool IsSynchroPickup(GachaBanner banner)
        {
            return banner.Pickup == PickupKind.Synchro && banner.LegendRate >= 0f && banner.SynchroRate >= 0f;
        }

        // 비어 있으면 모두 0. 합이 1이 아니면 비율을 유지한 채 맞춘다.
        public static void FixedRates(float[] source, float[] into)
        {
            var total = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                into[r] = source != null && r < source.Length ? Math.Max(0f, source[r]) : 0f;
                total += into[r];
            }

            for (var r = 0; r < RarityCount; r++)
            {
                into[r] = total > 0f ? into[r] / total : 0f;
            }
        }

        // 픽업 배너: 머신 풀과 머신 천장 카운터를 그대로 쓰고, 픽업 등급이 나오면 픽업 장비 쪽으로 기운다.
        public List<GearData> PullPickup(GachaBanner banner, int count, ref int counter, bool boost = true)
        {
            var machine = banner != null ? banner.Machine : null;
            if (machine == null)
            {
                throw new ArgumentNullException(nameof(banner));
            }

            var results = new List<GearData>(Math.Max(0, count));
            var guarantee = GuaranteeFor(count);
            var met = guarantee == ItemRarity.Normal;
            for (var i = 0; i < count; i++)
            {
                var floor = !met && i == count - 1 ? guarantee : ItemRarity.Normal;
                Rates(banner, counter, boost, _weights);
                var rarity = Roll(_weights, floor);
                counter = NextCounter(machine, counter, rarity);
                if (rarity >= guarantee)
                {
                    met = true;
                }

                results.Add(PickPickup(banner, rarity));
            }

            return results;
        }

        GearData PickPickup(GachaBanner banner, ItemRarity rarity)
        {
            var featured = banner.Featured;
            if (banner.Pickup == PickupKind.Legend && featured != null && featured.Rarity == rarity)
            {
                if (_random.Next() < banner.FeaturedShare)
                {
                    return featured;
                }

                return PickOfRarity(banner.Machine.Gears, rarity, featured) ?? featured;
            }

            return PickGear(banner.Machine.Gears, rarity);
        }

        // 정해진 등급 확률로 풀에서 뽑는다. 머신 천장 카운터는 쓰지 않는다(축제·데일리). 등급을 먼저 정하고 그 등급 장비 중 균등하게 고른다.
        // floor: 모든 뽑기의 최소 등급. pityIndex: 이 묶음에서 그때까지 pityMin 이상이 없었다면 pityMin 이상으로 확정할 순번(-1이면 없음).
        public List<GearData> PullPool(IReadOnlyList<GearData> pool, float[] rates, int count, ItemRarity floor = ItemRarity.Normal,
            int pityIndex = -1, ItemRarity pityMin = ItemRarity.Legend)
        {
            if (pool == null || rates == null)
            {
                throw new ArgumentNullException(pool == null ? nameof(pool) : nameof(rates));
            }

            var results = new List<GearData>(Math.Max(0, count));
            var guarantee = GuaranteeFor(count);
            var met = guarantee <= floor;
            var pityMet = false;
            for (var i = 0; i < count; i++)
            {
                var min = floor;
                if (!met && i == count - 1 && guarantee > min)
                {
                    min = guarantee;
                }

                if (!pityMet && i == pityIndex && pityMin > min)
                {
                    min = pityMin;
                }

                Array.Copy(rates, _weights, RarityCount);
                var rarity = Roll(_weights, min);
                if (rarity >= guarantee)
                {
                    met = true;
                }

                if (rarity >= pityMin)
                {
                    pityMet = true;
                }

                results.Add(PickGear(pool, rarity));
            }

            return results;
        }

        public List<GearData> Pull(GachaMachineData machine, int count, ref int counter, bool boost = true)
        {
            if (machine == null)
            {
                throw new ArgumentNullException(nameof(machine));
            }

            var results = new List<GearData>(Math.Max(0, count));
            var guarantee = GuaranteeFor(count);
            var met = guarantee == ItemRarity.Normal;
            for (var i = 0; i < count; i++)
            {
                var floor = !met && i == count - 1 ? guarantee : ItemRarity.Normal;
                var rarity = RollRarity(machine, counter, boost, floor);
                counter = NextCounter(machine, counter, rarity);
                if (rarity >= guarantee)
                {
                    met = true;
                }

                results.Add(PickGear(machine, rarity));
            }

            return results;
        }

        public ItemRarity RollRarity(GachaMachineData machine, int counter, bool boost, ItemRarity floor)
        {
            Rates(machine, counter, boost, _weights);
            return Roll(_weights, floor);
        }

        // weights는 floor 아래 등급을 0으로 지우며 바뀐다.
        ItemRarity Roll(float[] weights, ItemRarity floor)
        {
            var total = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                if (r < (int)floor)
                {
                    weights[r] = 0f;
                }

                total += weights[r];
            }

            if (total <= 0f)
            {
                return floor;
            }

            var roll = _random.Next() * total;
            var accumulated = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                accumulated += weights[r];
                if (weights[r] > 0f && roll < accumulated)
                {
                    return (ItemRarity)r;
                }
            }

            for (var r = RarityCount - 1; r >= 0; r--)
            {
                if (weights[r] > 0f)
                {
                    return (ItemRarity)r;
                }
            }

            return floor;
        }

        public GearData PickGear(GachaMachineData machine, ItemRarity rarity)
        {
            return PickGear(machine.Gears, rarity);
        }

        // 해당 등급 장비가 풀에 없으면 가까운 낮은 등급, 그다음 높은 등급에서 고른다.
        public GearData PickGear(IReadOnlyList<GearData> gears, ItemRarity rarity)
        {
            for (var distance = 0; distance < RarityCount; distance++)
            {
                var lower = (int)rarity - distance;
                if (lower >= 0)
                {
                    var gear = PickOfRarity(gears, (ItemRarity)lower);
                    if (gear != null)
                    {
                        return gear;
                    }
                }

                var upper = (int)rarity + distance;
                if (distance > 0 && upper < RarityCount)
                {
                    var gear = PickOfRarity(gears, (ItemRarity)upper);
                    if (gear != null)
                    {
                        return gear;
                    }
                }
            }

            return null;
        }

        GearData PickOfRarity(IReadOnlyList<GearData> gears, ItemRarity rarity, GearData exclude = null)
        {
            var matches = 0;
            for (var i = 0; i < gears.Count; i++)
            {
                if (gears[i] != null && gears[i].Rarity == rarity && gears[i] != exclude)
                {
                    matches++;
                }
            }

            if (matches == 0)
            {
                return null;
            }

            var pick = Math.Min(matches - 1, (int)(_random.Next() * matches));
            for (var i = 0; i < gears.Count; i++)
            {
                if (gears[i] == null || gears[i].Rarity != rarity || gears[i] == exclude)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return gears[i];
                }

                pick--;
            }

            return null;
        }

        static float Normalized(GachaMachineData machine, ItemRarity rarity)
        {
            var total = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                total += Math.Max(0f, machine.BaseRate((ItemRarity)r));
            }

            return total > 0f ? Math.Max(0f, machine.BaseRate(rarity)) / total : 0f;
        }

        static double AverageRefund(GachaCatalog catalog, IReadOnlyList<GearData> pool, ItemRarity rarity)
        {
            var sum = 0.0;
            var count = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].Rarity == rarity)
                {
                    sum += catalog.RefundValue(pool[i]);
                    count++;
                }
            }

            return count > 0 ? sum / count : 0.0;
        }

        static float AverageRefundMultiplier(GachaMachineData machine, ItemRarity rarity)
        {
            var sum = 0f;
            var count = 0;
            var gears = machine.Gears;
            for (var i = 0; i < gears.Count; i++)
            {
                if (gears[i] != null && gears[i].Rarity == rarity)
                {
                    sum += gears[i].RefundMultiplier;
                    count++;
                }
            }

            return count > 0 ? sum / count : GearData.DefaultRefundMultiplier(rarity);
        }
    }
}
