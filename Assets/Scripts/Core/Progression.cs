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
        const string DollarKey = "Dollar";

        static readonly float[] DepthLimits = { 0.49f, 0.62f, 0.74f, 0.87f, 1f };

        public static event Action Changed;

        public static int Dollars => PlayerPrefs.GetInt(DollarKey, 0);
        public static float CatchRadiusBonus => Level(UpgradeKind.Reel) * 0.12f;
        public static float ChipMultiplier => 1f + Level(UpgradeKind.Boat) * 0.1f;
        public static float MaxDepth => DepthLimits[Mathf.Clamp(Level(UpgradeKind.Depth), 0, DepthLimits.Length - 1)];

        public static int Level(UpgradeKind kind)
        {
            return PlayerPrefs.GetInt(LevelKey(kind), 0);
        }

        public static int MaxLevel(UpgradeKind kind)
        {
            return kind == UpgradeKind.Depth ? DepthLimits.Length - 1 : 5;
        }

        public static bool IsMaxed(UpgradeKind kind)
        {
            return Level(kind) >= MaxLevel(kind);
        }

        public static int Cost(UpgradeKind kind)
        {
            return BaseCost(kind) * (Level(kind) + 1);
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

        static int BaseCost(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Reel: return 60;
                case UpgradeKind.Boat: return 80;
                default: return 100;
            }
        }

        static string LevelKey(UpgradeKind kind)
        {
            return "Upgrade" + kind;
        }
    }
}
