using System;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // 장착한 장비 효과의 합계. 오리파·보관함에서 장착이 바뀌면 Refresh를 부르고, 낚시·잠수 코드는 여기서 값을 읽는다.
    // 장착은 타이틀·결과 화면에서만 바꿀 수 있으므로 "다음 출항부터 반영"이 된다.
    public static class GearBonuses
    {
        static readonly float[] Totals = new float[Enum.GetValues(typeof(GearStat)).Length];
        static readonly GearData[] EquippedGear = new GearData[GearInventory.SlotCount];

        public static event Action Changed;

        public static float Get(GearStat stat)
        {
            return Totals[(int)stat];
        }

        // 슬롯에 장착한 장비(없으면 null). 피셔 틴트처럼 등급을 보여 줄 때 쓴다.
        public static GearData Equipped(GearSlot slot)
        {
            var index = (int)slot;
            return index >= 0 && index < EquippedGear.Length ? EquippedGear[index] : null;
        }

        public static int Count(GearStat stat)
        {
            return Mathf.RoundToInt(Get(stat));
        }

        // 낚싯대 장비의 최대 수심: 강화로 정한 수심에서 바닥(1.0)까지 남은 거리의 %만큼 더 내려간다.
        public static float MaxDepth(float baseDepth)
        {
            var depth = Mathf.Clamp01(baseDepth);
            return depth + (1f - depth) * Mathf.Clamp01(Get(GearStat.MaxDepthPercent) / 100f);
        }

        // % 효과를 배수로 바꾼다. 음수(게이지 느려짐)도 0.2배 아래로는 내려가지 않는다.
        public static float Scale(GearStat stat)
        {
            return Mathf.Max(0.2f, 1f + Get(stat) / 100f);
        }

        public static void Refresh(GearInventory inventory)
        {
            Array.Clear(Totals, 0, Totals.Length);
            Array.Clear(EquippedGear, 0, EquippedGear.Length);
            if (inventory != null)
            {
                for (var s = 0; s < GearInventory.SlotCount; s++)
                {
                    var gear = inventory.Equipped((GearSlot)s);
                    EquippedGear[s] = gear;
                    Add(gear);
                }
            }

            Changed?.Invoke();
        }

        public static void Clear()
        {
            Array.Clear(Totals, 0, Totals.Length);
            Array.Clear(EquippedGear, 0, EquippedGear.Length);
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
                case GearStat.LureCapacity: return Loc.F("ui.effect.lure_capacity", Signed(v));
                case GearStat.CatchRadiusPercent: return Loc.F("ui.effect.catch_radius", Signed(v));
                case GearStat.RareChancePercent: return Loc.F("ui.effect.rare_chance", Signed(v));
                case GearStat.SpawnCapBonus: return Loc.F("ui.effect.spawn_cap", Signed(v));
                case GearStat.SellPercent: return Loc.F("ui.effect.sell", Signed(v));
                case GearStat.GaugeSpeedPercent: return Loc.F("ui.effect.gauge_speed", Signed(v));
                case GearStat.EndBonusPercent: return Loc.F("ui.effect.end_bonus", Signed(v));
                case GearStat.DroneRadiusPercent: return Loc.F("ui.effect.drone_radius", Signed(v));
                case GearStat.HazardWarning: return Loc.T("ui.effect.hazard_warning");
                case GearStat.AutoCollectSeconds: return Loc.F("ui.effect.auto_collect", v.ToString("0"));
                case GearStat.HazardShield: return Loc.F("ui.effect.hazard_shield", v.ToString("0"));
                case GearStat.OxygenPercent: return Loc.F("ui.effect.oxygen", Signed(v));
                case GearStat.HazardDamageReductionPercent: return v >= 100f ? Loc.T("ui.effect.hazard_immune") : Loc.F("ui.effect.hazard_reduce", v.ToString("0"));
                case GearStat.SteerSpeedPercent: return Loc.F("ui.effect.steer_speed", Signed(v));
                case GearStat.ReelSpeedPercent: return Loc.F("ui.effect.reel_speed", Signed(v));
                case GearStat.DiveSpeedPercent: return Loc.F("ui.effect.dive_speed", Signed(v));
                case GearStat.DiveEveryCastsBonus: return Loc.F("ui.effect.dive_every", v.ToString("0"));
                case GearStat.HookHitRadiusPercent: return Loc.F("ui.effect.hook_radius", Signed(v));
                case GearStat.MaxDepthPercent: return Loc.F("ui.effect.max_depth", Signed(v));
                case GearStat.BonusCatchChancePercent: return Loc.F("ui.effect.bonus_catch", v.ToString("0"));
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
