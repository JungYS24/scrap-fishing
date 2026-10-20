using UnityEngine;

namespace ScrapFishing.Audio
{
    public static class SfxLoudness
    {
        public const float QuietPeak = 0.05f;

        public static bool TryMeasurePeak(AudioClip clip, out float peak)
        {
            peak = 0f;
            if (clip == null || clip.samples <= 0)
            {
                return false;
            }

            if (!clip.LoadAudioData())
            {
                return false;
            }

            var data = new float[Mathf.Min(clip.samples * clip.channels, 48000)];
            if (!clip.GetData(data, 0))
            {
                return false;
            }

            var max = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var mag = Mathf.Abs(data[i]);
                if (mag > max)
                {
                    max = mag;
                }
            }

            peak = max;
            return true;
        }

        public static float PeakDb(float peak)
        {
            if (peak <= 0.00001f)
            {
                return -80f;
            }

            return 20f * Mathf.Log10(peak);
        }

        public static bool IsTooQuiet(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0)
            {
                return true;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if (!TryMeasurePeak(clip, out var peak))
            {
                return false;
            }

            return peak < QuietPeak;
#endif
        }
    }
}
