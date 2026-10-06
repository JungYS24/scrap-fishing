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

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
