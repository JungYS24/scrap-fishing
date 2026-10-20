using UnityEngine;

namespace ScrapFishing.Audio
{
    [CreateAssetMenu(menuName = "ScrapFishing/Sound Cue")]
    public class SoundCue : ScriptableObject
    {
        public SfxId id;
        public AudioClip[] clips = System.Array.Empty<AudioClip>();
        public SoundBus bus = SoundBus.Game;
        [Range(0f, 1f)] public float volume = 0.5f;
        public float pitchMin = 1f;
        public float pitchMax = 1f;
        public float cooldown;
        public int maxPerFrame = 2;
        public SoundCue fallback;
        public float fallbackPitch = 1f;
    }
}
