using ScrapFishing.Fishing;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    public static class RodStatsMenu
    {
        [MenuItem("Tools/Scrap Fishing/Rod/Level Up")]
        public static void LevelUp()
        {
            if (RodStats.TryUpgrade())
            {
                Debug.Log("RodLevel " + RodStats.Level + " / MaxAttach " + RodStats.MaxAttach);
                return;
            }

            Debug.Log("RodLevel 최대: " + RodStats.Level + " / MaxAttach " + RodStats.MaxAttach);
        }

        [MenuItem("Tools/Scrap Fishing/Rod/Reset")]
        public static void ResetLevel()
        {
            RodStats.ResetLevel();
            Debug.Log("RodLevel 0 / MaxAttach " + RodStats.MaxAttach);
        }
    }
}
