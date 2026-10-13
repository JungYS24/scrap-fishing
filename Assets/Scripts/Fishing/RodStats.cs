using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Fishing
{
    public static class RodStats
    {
        public const string LevelKey = "RodLevel";

        static readonly int[] CapacityByLevel = { 4, 5, 6, 7, 8, 9 };

        public static int Level => Mathf.Max(0, PlayerPrefs.GetInt(LevelKey, 0));

        public static int MaxLevel => CapacityByLevel.Length - 1;

        public static int MaxAttach => LureLoad.CapacityForLevel(Progression.Level(UpgradeKind.Reel), CapacityByLevel);

        public static bool TryUpgrade()
        {
            if (Level >= MaxLevel)
            {
                return false;
            }

            PlayerPrefs.SetInt(LevelKey, Level + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetLevel()
        {
            PlayerPrefs.SetInt(LevelKey, 0);
            PlayerPrefs.Save();
        }
    }
}
