using System;
using System.Collections.Generic;
using ScrapFishing.Data;

namespace ScrapFishing.Gacha
{
    // 뽑기 계산만 담당한다. 저장·달러·UI는 모른다.
    public sealed class GachaService
    {
        public const int TenPull = 10;
        public const int HundredPull = 100;
        public const int RarityCount = 5;

        readonly IGachaRandom _random;
        readonly float[] _weights = new float[RarityCount];

        public GachaService(IGachaRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        // 10연은 Rare 이상, 100연은 Epic 이상 1개를 보장한다. 그 외(1회 등)는 보장 없음.
        public static ItemRarity GuaranteeFor(int count)
        {
            if (count >= HundredPull)
            {
                return ItemRarity.Epic;
            }

            return count >= TenPull ? ItemRarity.Rare : ItemRarity.Normal;
        }

        // counter: 이 머신에서 Legend(또는 Synchro) 없이 뽑은 횟수(주기 안). 이번 뽑기는 counter+1 번째다.
        public static float LegendRate(GachaMachineData machine, int counter, bool boost = true)
        {
            var baseRate = Normalized(machine, ItemRarity.Legend);
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
            var legend = LegendRate(machine, counter, boost);
            var synchro = Normalized(machine, ItemRarity.Synchro);
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
            var cycle = Math.Max(1, machine.BoostCycle);
            var rates = new float[RarityCount];
            var step = new float[RarityCount];
            var survive = 1.0;
            var total = 0.0;
            var sums = new double[RarityCount];
            for (var counter = 0; counter < cycle; counter++)
            {
                Rates(machine, counter, true, step);
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

        // 1회 가격 대비 환원 기댓값(보간 포함, 10연·100연 보장 제외).
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
            var total = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                if (r < (int)floor)
                {
                    _weights[r] = 0f;
                }

                total += _weights[r];
            }

            if (total <= 0f)
            {
                return floor;
            }

            var roll = _random.Next() * total;
            var accumulated = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                accumulated += _weights[r];
                if (_weights[r] > 0f && roll < accumulated)
                {
                    return (ItemRarity)r;
                }
            }

            for (var r = RarityCount - 1; r >= 0; r--)
            {
                if (_weights[r] > 0f)
                {
                    return (ItemRarity)r;
                }
            }

            return floor;
        }

        // 해당 등급 장비가 머신에 없으면 가까운 낮은 등급, 그다음 높은 등급에서 고른다.
        public GearData PickGear(GachaMachineData machine, ItemRarity rarity)
        {
            for (var distance = 0; distance < RarityCount; distance++)
            {
                var lower = (int)rarity - distance;
                if (lower >= 0)
                {
                    var gear = PickOfRarity(machine, (ItemRarity)lower);
                    if (gear != null)
                    {
                        return gear;
                    }
                }

                var upper = (int)rarity + distance;
                if (distance > 0 && upper < RarityCount)
                {
                    var gear = PickOfRarity(machine, (ItemRarity)upper);
                    if (gear != null)
                    {
                        return gear;
                    }
                }
            }

            return null;
        }

        GearData PickOfRarity(GachaMachineData machine, ItemRarity rarity)
        {
            var gears = machine.Gears;
            var matches = 0;
            for (var i = 0; i < gears.Count; i++)
            {
                if (gears[i] != null && gears[i].Rarity == rarity)
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
                if (gears[i] == null || gears[i].Rarity != rarity)
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
