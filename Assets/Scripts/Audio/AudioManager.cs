using UnityEngine;

namespace ScrapFishing.Audio
{
    public class AudioManager : MonoBehaviour
    {
        const string BgmResource = "Audio/Neo-Tokyo Sludge";
        const float BgmVolume = 0.55f;

        public static AudioManager Instance { get; private set; }

        AudioSource _bgm;
        AudioSource _sfx;
        AudioClip _catchClip;
        AudioClip _hurtClip;
        AudioClip _castClip;
        AudioClip _reelClip;
        AudioClip _diveClip;
        AudioClip _splashClip;
        AudioClip _diveReadyClip;
        AudioClip[] _zoneClips;

        public static AudioManager Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.loop = true;
            _bgm.spatialBlend = 0f;
            _bgm.volume = BgmVolume;
            _bgm.ignoreListenerPause = true;
            _bgm.clip = Resources.Load<AudioClip>(BgmResource);

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.loop = false;
            _sfx.spatialBlend = 0f;
            _sfx.ignoreListenerPause = true;
            _catchClip = MakeTone(880, 0.09f);
            _hurtClip = MakeTone(180, 0.16f);
            _castClip = MakeTone(520, 0.07f);
            _reelClip = MakeTone(340, 0.1f);
            _diveClip = MakeTone(260, 0.14f);
            _splashClip = MakeSplash(0.16f);
            _diveReadyClip = MakeChime(660, 990, 0.08f);
            _zoneClips = new[]
            {
                MakeTone(400, 0.07f),
                MakeTone(520, 0.07f),
                MakeTone(660, 0.08f),
                MakeTone(820, 0.09f)
            };
        }

        public void PlayBgm()
        {
            if (_bgm == null || _bgm.clip == null || _bgm.isPlaying)
            {
                return;
            }

            _bgm.Play();
        }

        public void PlayCatch()
        {
            PlaySfx(_catchClip, 0.45f);
        }

        public void PlayHurt()
        {
            PlaySfx(_hurtClip, 0.5f);
        }

        public void PlayCast()
        {
            PlaySfx(_castClip, 0.35f);
        }

        public void PlayReel()
        {
            PlaySfx(_reelClip, 0.32f);
        }

        public void PlayDive()
        {
            PlaySfx(_diveClip, 0.4f);
        }

        public void PlaySplash()
        {
            PlaySfx(_splashClip, 0.4f);
        }

        public void PlayDiveReady()
        {
            PlaySfx(_diveReadyClip, 0.4f);
        }

        public void PlayZone(int index)
        {
            if (_zoneClips == null || _zoneClips.Length == 0)
            {
                return;
            }

            var i = Mathf.Clamp(index, 0, _zoneClips.Length - 1);
            PlaySfx(_zoneClips[i], 0.28f);
        }

        void PlaySfx(AudioClip clip, float volume)
        {
            if (_sfx == null || clip == null)
            {
                return;
            }

            _sfx.PlayOneShot(clip, volume);
        }

        static AudioClip MakeTone(int hertz, float seconds)
        {
            const int sampleRate = 22050;
            var samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * seconds));
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = 1f - t / seconds;
                data[i] = Mathf.Sin(2f * Mathf.PI * hertz * t) * envelope * 0.35f;
            }

            var clip = AudioClip.Create("tone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeChime(int lowHertz, int highHertz, float noteSeconds)
        {
            const int sampleRate = 22050;
            var noteSamples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * noteSeconds));
            var data = new float[noteSamples * 2];
            for (var i = 0; i < data.Length; i++)
            {
                var high = i >= noteSamples;
                var t = (high ? i - noteSamples : i) / (float)sampleRate;
                var envelope = 1f - t / noteSeconds;
                data[i] = Mathf.Sin(2f * Mathf.PI * (high ? highHertz : lowHertz) * t) * envelope * 0.35f;
            }

            var clip = AudioClip.Create("chime", data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeSplash(float seconds)
        {
            const int sampleRate = 22050;
            var samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * seconds));
            var data = new float[samples];
            var noise = new System.Random(7);
            var phase = 0f;
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = 1f - t / seconds;
                envelope *= envelope;
                phase += 2f * Mathf.PI * Mathf.Lerp(900f, 220f, t / seconds) / sampleRate;
                var hiss = (float)noise.NextDouble() * 2f - 1f;
                data[i] = (Mathf.Sin(phase) * 0.6f + hiss * 0.4f) * envelope * 0.35f;
            }

            var clip = AudioClip.Create("splash", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
