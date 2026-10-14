using UnityEngine;

namespace ScrapFishing.Audio
{
    [CreateAssetMenu(menuName = "ScrapFishing/AudioMix")]
    public class AudioMix : ScriptableObject
    {
        [Header("Master")]
        public float masterSfx = 1f;
        public float masterBgm = 1f;
        public float titleBgm = 0.5f;
        public float playBgm = 0.55f;
        public float resultsDuck = 0.5f;
        public float diveDuck = 0.72f;
        public float crossfade = 0.6f;

        [Header("SFX")]
        public float uiTap = 0.6f;
        public float start = 0.5f;
        public float castWhoosh = 0.55f;
        public float splash = 0.65f;
        public float reel = 1f;
        public float zoneEnter = 0.5f;
        public float catchCommon = 0.5f;
        public float catchRare = 0.8f;
        public float catchMutant = 0.85f;
        public float chipTick = 0.35f;
        public float diveReady = 0.45f;
        public float timeUp = 0.5f;
        public float timerTick = 0.35f;
        public float upgradeBuy = 0.65f;
        public float upgradeDenied = 0.8f;
        public float fullDenied = 0.3f;
        public float hazard = 0.5f;
        public float diveEnter = 0.4f;
        public float mineHit = 0.5f;
        public float oxygenWarn = 0.45f;
        public float surface = 0.4f;
        public float newBest = 0.5f;

        public float Named(string key)
        {
            switch (key)
            {
                case "sfx_ui_tap": return uiTap;
                case "sfx_start": return start;
                case "sfx_cast_whoosh": return castWhoosh;
                case "sfx_splash": return splash;
                case "sfx_reel": return reel;
                case "sfx_zone_enter": return zoneEnter;
                case "sfx_catch_common": return catchCommon;
                case "sfx_catch_rare": return catchRare;
                case "sfx_catch_mutant": return catchMutant;
                case "sfx_chip_tick": return chipTick;
                case "sfx_dive_ready": return diveReady;
                case "sfx_time_up": return timeUp;
                case "sfx_timer_tick": return timerTick;
                case "sfx_upgrade_buy": return upgradeBuy;
                case "sfx_upgrade_denied": return upgradeDenied;
                case "sfx_hazard": return hazard;
                case "sfx_dive_enter": return diveEnter;
                case "sfx_mine_hit": return mineHit;
                case "sfx_oxygen_warn": return oxygenWarn;
                case "sfx_surface": return surface;
                case "sfx_new_best": return newBest;
                default: return 0.5f;
            }
        }
    }
}
