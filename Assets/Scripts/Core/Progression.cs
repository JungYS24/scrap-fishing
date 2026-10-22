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
        public const int MaxUpgradeLevel = 12;

        public const string DollarKey = "Dollar";
        const float ReelRadiusPerLevel = 0.05f;
        const float BoatChipPerLevel = 0.05f;
        const float CostGrowth = 1.38f;
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
            return Mathf.Max(1, Mathf.RoundToInt(BaseCost(kind) * Mathf.Pow(CostGrowth, level)));
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

            return DepthZone.Name(nextDepth) + " 해금";
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
                return new GoalHint("모든 강화 완료! 최고 기록에 도전하세요", null, false, true);
            }

            var primary = canBuy
                ? "지금 [" + ShortName(buyKind) + "] 강화 가능!"
                : "[" + ShortName(nextKind) + "] 강화까지 $" + Mathf.Max(0, nextGap).ToString("N0") + " 남음";
            string zone = null;
            if (TryLevelsUntilNextZone(out var levels, out var zoneName))
            {
                zone = "수심 강화 " + levels + "번 더 → [" + zoneName + "] 해금";
            }

            return new GoalHint(primary, zone, canBuy, false);
        }

        public static string DisplayName(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Reel: return "릴 강화";
                case UpgradeKind.Boat: return "보트 강화";
                default: return "수심 강화";
            }
        }

        public static void AddDollars(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            PlayerPrefs.SetInt(DollarKey, Dollars + amount);
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
                case UpgradeKind.Reel: return "릴";
                case UpgradeKind.Boat: return "보트";
                default: return "수심";
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
