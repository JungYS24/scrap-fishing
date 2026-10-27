using System;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Core
{
    public enum UpgradeKind
    {
        Reel,
        Boat,
        Depth
    }

    public static class Progression
    {
        // 강화 30단계. 예전 12단계 효과 범위(반경 +0.6, 칩 +60%, 수심 1000m)를 30등분했다.
        // Lv12까지(구매 12번)는 예전 곡선(×1.38) 그대로이고, 그 뒤로는 ×1.10씩만 오른다.
        public const int MaxUpgradeLevel = 30;

        public const string DollarKey = "Dollar";
        const float ReelRadiusPerLevel = 0.6f / MaxUpgradeLevel;
        const float BoatChipPerLevel = 0.6f / MaxUpgradeLevel;
        const float CostGrowth = 1.38f;
        const int LegacyLevels = 12;
        const float LateCostGrowth = 1.10f;
        const int ReelBaseCost = 250;
        const int BoatBaseCost = 320;
        const int DepthBaseCost = 380;
        const float DepthFloor = 0.49f;
        const float DepthCeiling = 1f;

        static readonly float[] DepthLimits = BuildDepthLimits();

        public static event Action Changed;

        public static int Dollars => PlayerPrefs.GetInt(DollarKey, 0);
        public static float CatchRadiusBonus => Level(UpgradeKind.Reel) * ReelRadiusPerLevel;
        public static float ChipMultiplier => 1f + Level(UpgradeKind.Boat) * BoatChipPerLevel;
        public static float MaxDepth => DepthAt(Level(UpgradeKind.Depth));

        public static int Level(UpgradeKind kind)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(LevelKey(kind), 0));
        }

        public static int MaxLevel(UpgradeKind kind)
        {
            return MaxUpgradeLevel;
        }

        public static bool IsMaxed(UpgradeKind kind)
        {
            return Level(kind) >= MaxLevel(kind);
        }

        public static int Cost(UpgradeKind kind)
        {
            return CostAt(kind, Level(kind));
        }

        public static int CostAt(UpgradeKind kind, int level)
        {
            level = Mathf.Max(0, level);
            var early = Mathf.Min(level, LegacyLevels);
            var late = level - early;
            return Mathf.Max(1, Mathf.RoundToInt(BaseCost(kind) * Mathf.Pow(CostGrowth, early) * Mathf.Pow(LateCostGrowth, late)));
        }

        public static void MigrateSave()
        {
            if (SaveMigration.Run(PlayerPrefsStore.Instance))
            {
                Changed?.Invoke();
            }
        }

        public static string ZoneUnlockLabel(UpgradeKind kind)
        {
            if (kind != UpgradeKind.Depth || IsMaxed(kind))
            {
                return null;
            }

            var currentZone = DepthZone.Index(DepthAt(Level(kind)));
            var nextDepth = DepthAt(Level(kind) + 1);
            if (DepthZone.Index(nextDepth) <= currentZone)
            {
                return null;
            }

            return Loc.F("ui.upgrade.zone_unlock", DepthZone.Name(nextDepth));
        }

        public readonly struct GoalHint
        {
            public readonly string Primary;
            public readonly string Zone;
            public readonly bool CanBuy;
            public readonly bool AllMaxed;

            public GoalHint(string primary, string zone, bool canBuy, bool allMaxed)
            {
                Primary = primary;
                Zone = zone;
                CanBuy = canBuy;
                AllMaxed = allMaxed;
            }
        }

        public static GoalHint NextGoal()
        {
            var allMaxed = true;
            var canBuy = false;
            var buyKind = UpgradeKind.Reel;
            var buyCost = int.MaxValue;
            var nextKind = UpgradeKind.Reel;
            var nextGap = int.MaxValue;
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
            {
                if (IsMaxed(kind))
                {
                    continue;
                }

                allMaxed = false;
                var cost = Cost(kind);
                if (Dollars >= cost && cost < buyCost)
                {
                    canBuy = true;
                    buyCost = cost;
                    buyKind = kind;
                }

                var gap = cost - Dollars;
                if (gap < nextGap)
                {
                    nextGap = gap;
                    nextKind = kind;
                }
            }

            if (allMaxed)
            {
                return new GoalHint(Loc.T("ui.upgrade.all_done"), null, false, true);
            }

            var primary = canBuy
                ? Loc.F("ui.upgrade.can_buy", ShortName(buyKind))
                : Loc.F("ui.upgrade.need_money", ShortName(nextKind), Mathf.Max(0, nextGap).ToString("N0"));
            string zone = null;
            if (TryLevelsUntilNextZone(out var levels, out var zoneName))
            {
                zone = Loc.F("ui.upgrade.zone_hint", levels, zoneName);
            }

            return new GoalHint(primary, zone, canBuy, false);
        }

        public static string DisplayName(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Reel: return Loc.T("ui.upgrade.reel");
                case UpgradeKind.Boat: return Loc.T("ui.upgrade.boat");
                default: return Loc.T("ui.upgrade.depth");
            }
        }

        public static void AddDollars(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            // 쿠폰을 여러 번 쓰면 int 최대값 근처까지 갈 수 있어서, 넘쳐 음수가 되지 않게 자른다.
            PlayerPrefs.SetInt(DollarKey, (int)System.Math.Min(int.MaxValue, (long)Dollars + amount));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0 || Dollars < amount)
            {
                return false;
            }

            PlayerPrefs.SetInt(DollarKey, Dollars - amount);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(DollarKey);
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
            {
                PlayerPrefs.DeleteKey(LevelKey(kind));
            }

            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool TryBuy(UpgradeKind kind)
        {
            if (IsMaxed(kind))
            {
                return false;
            }

            var cost = Cost(kind);
            if (Dollars < cost)
            {
                return false;
            }

            PlayerPrefs.SetInt(DollarKey, Dollars - cost);
            PlayerPrefs.SetInt(LevelKey(kind), Level(kind) + 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        static float DepthAt(int level)
        {
            var index = Mathf.Clamp(level, 0, DepthLimits.Length - 1);
            return DepthLimits[index];
        }

        static float[] BuildDepthLimits()
        {
            var limits = new float[MaxUpgradeLevel + 1];
            for (var i = 0; i < limits.Length; i++)
            {
                var t = MaxUpgradeLevel <= 0 ? 1f : i / (float)MaxUpgradeLevel;
                limits[i] = Mathf.Lerp(DepthFloor, DepthCeiling, t);
            }

            return limits;
        }

        static int BaseCost(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Reel: return ReelBaseCost;
                case UpgradeKind.Boat: return BoatBaseCost;
                default: return DepthBaseCost;
            }
        }

        public static string LevelKey(UpgradeKind kind)
        {
            return "Upgrade" + kind;
        }

        static string ShortName(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Reel: return Loc.T("ui.upgrade.short.reel");
                case UpgradeKind.Boat: return Loc.T("ui.upgrade.short.boat");
                default: return Loc.T("ui.upgrade.short.depth");
            }
        }

        static bool TryLevelsUntilNextZone(out int levels, out string zoneName)
        {
            levels = 0;
            zoneName = null;
            if (IsMaxed(UpgradeKind.Depth))
            {
                return false;
            }

            var current = DepthZone.Index(DepthAt(Level(UpgradeKind.Depth)));
            var level = Level(UpgradeKind.Depth);
            for (var next = level + 1; next <= MaxLevel(UpgradeKind.Depth); next++)
            {
                if (DepthZone.Index(DepthAt(next)) <= current)
                {
                    continue;
                }

                levels = next - level;
                zoneName = DepthZone.Name(DepthAt(next));
                return true;
            }

            return false;
        }
    }
}
