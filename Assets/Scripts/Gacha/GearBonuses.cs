using System;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // 장착한 장비 효과의 합계. 오리파·보관함에서 장착이 바뀌면 Refresh를 부르고, 낚시·잠수 코드는 여기서 값을 읽는다.
    // 장착은 타이틀·결과 화면에서만 바꿀 수 있으므로 "다음 출항부터 반영"이 된다.
    public static class GearBonuses
    {
        static readonly float[] Totals = new float[Enum.GetValues(typeof(GearStat)).Length];

        public static event Action Changed;

        public static float Get(GearStat stat)
        {
            return Totals[(int)stat];
        }

        public static int Count(GearStat stat)
        {
            return Mathf.RoundToInt(Get(stat));
        }

        // % 효과를 배수로 바꾼다. 음수(게이지 느려짐)도 0.2배 아래로는 내려가지 않는다.
        public static float Scale(GearStat stat)
        {
            return Mathf.Max(0.2f, 1f + Get(stat) / 100f);
        }

        public static void Refresh(GearInventory inventory)
        {
            Array.Clear(Totals, 0, Totals.Length);
            if (inventory != null)
            {
                for (var s = 0; s < GearInventory.SlotCount; s++)
                {
                    Add(inventory.Equipped((GearSlot)s));
                }
            }

            Changed?.Invoke();
        }

        public static void Clear()
        {
            Array.Clear(Totals, 0, Totals.Length);
            Changed?.Invoke();
        }

        static void Add(GearData gear)
        {
            if (gear == null)
            {
                return;
            }

            foreach (var effect in gear.Effects)
            {
                Totals[(int)effect.stat] += effect.value;
            }
        }

        // 보관함·결과 카드에 쓰는 한 줄 설명.
        public static string Describe(GearEffect effect)
        {
            var v = effect.value;
            switch (effect.stat)
            {
                case GearStat.LureCapacity: return "루어 용량 " + Signed(v) + "개";
                case GearStat.CatchRadiusPercent: return "흡착 반경 " + Signed(v) + "%";
                case GearStat.RareChancePercent: return "레어 이상 " + Signed(v) + "%p";
                case GearStat.SpawnCapBonus: return "스폰 상한 " + Signed(v) + "개";
                case GearStat.SellPercent: return "판매가 " + Signed(v) + "%";
                case GearStat.GaugeSpeedPercent: return "수심 게이지 " + Signed(v) + "%";
                case GearStat.EndBonusPercent: return "판 종료 보너스 " + Signed(v) + "%";
                case GearStat.DroneRadiusPercent: return "잠수 흡착 반경 " + Signed(v) + "%";
                case GearStat.HazardWarning: return "위험물 경고";
                case GearStat.AutoCollectSeconds: return "자동 회수 " + v.ToString("0") + "초마다";
                case GearStat.HazardShield: return "위험물 무효 " + v.ToString("0") + "회";
                case GearStat.OxygenPercent: return "산소 " + Signed(v) + "%";
                case GearStat.HazardDamageReductionPercent: return v >= 100f ? "위험물 피해 무효" : "위험물 피해 -" + v.ToString("0") + "%";
                case GearStat.SteerSpeedPercent: return "조종 속도 " + Signed(v) + "%";
                case GearStat.ReelSpeedPercent: return "릴 속도 " + Signed(v) + "%";
                case GearStat.DiveSpeedPercent: return "잠수 이동 " + Signed(v) + "%";
                case GearStat.DiveEveryCastsBonus: return "잠수 주기 -" + v.ToString("0") + "캐스트";
                default: return effect.stat + " " + Signed(v);
            }
        }

        public static string Describe(GearData gear, string separator = " · ")
        {
            if (gear == null || gear.Effects.Count == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < gear.Effects.Count; i++)
            {
                text += (i > 0 ? separator : string.Empty) + Describe(gear.Effects[i]);
            }

            return text;
        }

        static string Signed(float value)
        {
            return (value >= 0f ? "+" : string.Empty) + value.ToString("0");
        }
    }
}
