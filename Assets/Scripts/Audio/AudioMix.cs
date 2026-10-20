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
    }
}
