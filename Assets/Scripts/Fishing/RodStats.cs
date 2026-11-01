using ScrapFishing.Core;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Fishing
{
    public static class RodStats
    {
        public const string LevelKey = "RodLevel";

        static readonly int[] CapacityByLevel = { 4, 5, 6, 7, 8, 9 };

        public static int Level => Mathf.Max(0, SaveStore.GetInt(LevelKey, 0));

        public static int MaxLevel => CapacityByLevel.Length - 1;

        // 강화 레벨 용량 + 루어 장비 용량.
        public static int MaxAttach => LureLoad.CapacityForLevel(Progression.Level(UpgradeKind.Reel), CapacityByLevel)
            + Mathf.Max(0, GearBonuses.Count(GearStat.LureCapacity));

        public static bool TryUpgrade()
        {
            if (Level >= MaxLevel)
            {
                return false;
            }

            SaveStore.SetInt(LevelKey, Level + 1);
            SaveStore.Save();
            return true;
        }

        public static void ResetLevel()
        {
            SaveStore.SetInt(LevelKey, 0);
            SaveStore.Save();
        }
    }
}
