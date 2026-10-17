using System;
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

        const string DollarKey = "Dollar";
        const string SaveVersionKey = "SaveVersion";
        const int SaveVersion = 2;
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
            if (PlayerPrefs.GetInt(SaveVersionKey, 0) == SaveVersion)
            {
                return;
            }

            PlayerPrefs.DeleteKey(DollarKey);
            PlayerPrefs.DeleteKey("RodLevel");
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
            {
                PlayerPrefs.DeleteKey(LevelKey(kind));
            }

            PlayerPrefs.SetInt(SaveVersionKey, SaveVersion);
            PlayerPrefs.Save();
            Changed?.Invoke();
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

        static string LevelKey(UpgradeKind kind)
        {
            return "Upgrade" + kind;
        }
    }
}
