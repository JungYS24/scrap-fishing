using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Data;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace ScrapFishing.Audio
{
    public class AudioManager : MonoBehaviour
    {
        const string MixResource = "Audio/AudioMix";
        const string SfxFolder = "Audio/SFX";
        const string TitleBgmResource = "Audio/Start_Title_BGM";
        const string PlayBgmResource = "Audio/Neo-Tokyo Sludge";
        const int PoolSize = 6;
        const int MaxSameFrame = 2;
        const float QuietPeak = 0.05f;
        const float PitchJitter = 0.04f;
        const float FullCooldown = 0.5f;

        static readonly float[] ZonePitch = { 1f, 0.94f, 0.88f, 0.82f };
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "sfx_catch_mutant", "sfx_catch_rare" },
            { "sfx_dive_enter", "sfx_dive_ready" },
            { "sfx_hazard", "sfx_upgrade_denied" },
            { "sfx_mine_hit", "sfx_upgrade_denied" },
            { "sfx_oxygen_warn", "sfx_timer_tick" },
            { "sfx_surface", "sfx_splash" },
            { "sfx_new_best", "sfx_dive_ready" }
        };

        public static AudioManager Instance { get; private set; }

        [SerializeField] AudioMix mix;

        readonly Dictionary<string, List<AudioClip>> _clips = new Dictionary<string, List<AudioClip>>();
        readonly Dictionary<string, AudioClip> _fallback = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, int> _frameHits = new Dictionary<string, int>();
        readonly HashSet<string> _quiet = new HashSet<string>();
        AudioSource[] _pool;
        AudioSource _bgmA;
        AudioSource _bgmB;
        AudioSource _bgmLive;
        AudioClip _titleClip;
        AudioClip _playClip;
        int _poolAt;
        int _hitFrame = -1;
        float _bgmTarget = 0.55f;
        float _fullAt;
        bool _unlocked;
        bool _wantTitle;
        bool _diving;

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
            mix = Resources.Load<AudioMix>(MixResource);
            if (mix == null)
            {
                mix = ScriptableObject.CreateInstance<AudioMix>();
            }

            BuildFallbacks();
            LoadClips();
            _titleClip = Resources.Load<AudioClip>(TitleBgmResource);
            _playClip = Resources.Load<AudioClip>(PlayBgmResource);
            _bgmA = MakeBgm("BgmA");
            _bgmB = MakeBgm("BgmB");
            _bgmLive = _bgmA;
            _pool = new AudioSource[PoolSize];
            for (var i = 0; i < PoolSize; i++)
            {
                _pool[i] = MakeSfx("Sfx" + i);
            }
        }

        void Update()
        {
            if (_unlocked)
            {
                return;
            }

            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                Unlock();
            }
        }

        public void Unlock()
        {
            AudioListener.pause = false;
            _unlocked = true;
            if (_wantTitle)
            {
                PlayTitleBgm();
            }
        }

        public void PlayTitleBgm()
        {
            _wantTitle = true;
            _diving = false;
            Crossfade(_titleClip, mix.titleBgm * mix.masterBgm);
        }

        public void PlayPlayBgm()
        {
            _wantTitle = false;
            _diving = false;
            Crossfade(_playClip, mix.playBgm * mix.masterBgm);
        }

        public void PlayBgm()
        {
            PlayPlayBgm();
        }

        public void DuckResults()
        {
            _diving = false;
            FadeLive(_bgmTarget * mix.resultsDuck, 0.35f);
        }

        public void SetDiveMix(bool diving)
        {
            _diving = diving;
            if (_bgmLive == null)
            {
                return;
            }

            var vol = _bgmTarget * (diving ? mix.diveDuck : 1f);
            FadeLive(vol, 0.28f);
        }

        public void PlayUiTap()
        {
            PlayNamed("sfx_ui_tap", 1f, false);
        }

        public void PlayStart()
        {
            PlayNamed("sfx_start", 1f, false);
        }

        public void PlayCast()
        {
            PlayNamed("sfx_cast_whoosh", 1f, true);
        }

        public void PlaySplash()
        {
            PlayNamed("sfx_splash", 1f, false);
        }

        public void PlayReel()
        {
            PlayNamed("sfx_reel", 1f, false);
        }

        public void PlayZone(int index)
        {
            var i = Mathf.Clamp(index, 0, ZonePitch.Length - 1);
            PlayNamed("sfx_zone_enter", ZonePitch[i], false);
        }

        public void PlayCatch()
        {
            PlayCatchGrade(ScrapGrade.Common);
        }

        public void PlayCatchGrade(ScrapGrade grade)
        {
            if (grade >= ScrapGrade.Mutant)
            {
                if (HasClip("sfx_catch_mutant"))
                {
                    PlayNamed("sfx_catch_mutant", 1f, true);
                    return;
                }

                PlayNamed("sfx_catch_rare", 0.9f, true);
                return;
            }

            if (grade >= ScrapGrade.Rare)
            {
                PlayNamed("sfx_catch_rare", 1f, true);
                return;
            }

            PlayNamed("sfx_catch_common", 1f, true);
        }

        public void PlayCatch(ScrapView scrap)
        {
            PlayCatchGrade(GradeOf(scrap));
        }

        public void PlayHazard()
        {
            PlayNamed("sfx_hazard", 1f, false);
        }

        public void PlayChipArrive(float pitch)
        {
            PlayNamed("sfx_chip_tick", pitch, true);
        }

        public void PlayDiveReady()
        {
            PlayNamed("sfx_dive_ready", 1f, false);
        }

        public void PlayDive()
        {
            PlayNamed("sfx_dive_enter", 1f, false);
        }

        public void PlayHurt()
        {
            PlayNamed("sfx_mine_hit", 1f, false);
        }

        public void PlayMineHit()
        {
            PlayNamed("sfx_mine_hit", 1f, false);
        }

        public void PlayOxygenWarn()
        {
            PlayNamed("sfx_oxygen_warn", 1f, false);
        }

        public void PlaySurface()
        {
            PlayNamed("sfx_surface", 1f, false);
        }

        public void PlayTimerTick(float pitch)
        {
            PlayNamed("sfx_timer_tick", pitch, false);
        }

        public void PlayTimeUp()
        {
            PlayNamed("sfx_time_up", 1f, false);
        }

        public void PlayNewBest()
        {
            if (HasClip("sfx_new_best"))
            {
                PlayNamed("sfx_new_best", 1f, false);
                return;
            }

            PlayNamed("sfx_dive_ready", 1.1f, false);
        }

        public void PlayUpgradeBuy()
        {
            PlayNamed("sfx_upgrade_buy", 1f, false);
        }

        public void PlayUpgradeDenied()
        {
            PlayNamed("sfx_upgrade_denied", 1f, false);
        }

        public void PlayFull()
        {
            if (Time.unscaledTime < _fullAt)
            {
                return;
            }

            _fullAt = Time.unscaledTime + FullCooldown;
            PlayNamed("sfx_upgrade_denied", 1f, false, mix.fullDenied);
        }

        public void Preview(string key)
        {
            Unlock();
            PlayNamed(key, 1f, false);
        }

        void PlayNamed(string key, float pitch, bool jitter, float volumeOverride = -1f)
        {
            if (Time.frameCount != _hitFrame)
            {
                _hitFrame = Time.frameCount;
                _frameHits.Clear();
            }

            _frameHits.TryGetValue(key, out var hits);
            if (hits >= MaxSameFrame)
            {
                return;
            }

            _frameHits[key] = hits + 1;
            var clip = Resolve(key);
            if (clip == null)
            {
                return;
            }

            var src = NextVoice();
            if (src == null)
            {
                return;
            }

            var vol = (volumeOverride >= 0f ? volumeOverride : mix.Named(key)) * mix.masterSfx;
            var usedPitch = Mathf.Clamp(pitch, 0.5f, 2f);
            if (jitter)
            {
                usedPitch *= Random.Range(1f - PitchJitter, 1f + PitchJitter);
            }

            src.Stop();
            src.clip = clip;
            src.pitch = usedPitch;
            src.volume = vol;
            src.Play();
        }

        AudioClip Resolve(string key)
        {
            if (_clips.TryGetValue(key, out var list) && list.Count > 0)
            {
                return list[Random.Range(0, list.Count)];
            }

            if (Aliases.TryGetValue(key, out var alias) && _clips.TryGetValue(alias, out var mapped) && mapped.Count > 0)
            {
                return mapped[Random.Range(0, mapped.Count)];
            }

            _fallback.TryGetValue(key, out var tone);
            return tone;
        }

        bool HasClip(string key)
        {
            if (_clips.TryGetValue(key, out var list) && list.Count > 0)
            {
                return true;
            }

            return Aliases.TryGetValue(key, out var alias) && _clips.TryGetValue(alias, out var mapped) && mapped.Count > 0;
        }

        AudioSource NextVoice()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                var src = _pool[(_poolAt + i) % _pool.Length];
                if (src != null && !src.isPlaying)
                {
                    _poolAt = (_poolAt + i + 1) % _pool.Length;
                    return src;
                }
            }

            var steal = _pool[_poolAt];
            _poolAt = (_poolAt + 1) % _pool.Length;
            return steal;
        }

        void LoadClips()
        {
            var loaded = Resources.LoadAll<AudioClip>(SfxFolder);
            for (var i = 0; i < loaded.Length; i++)
            {
                var clip = loaded[i];
                if (clip == null)
                {
                    continue;
                }

                var key = VariantKey(clip.name);
                if (IsTooQuiet(clip))
                {
                    _quiet.Add(key);
                    continue;
                }

                if (!_clips.TryGetValue(key, out var list))
                {
                    list = new List<AudioClip>(3);
                    _clips[key] = list;
                }

                list.Add(clip);
            }
        }

        static string VariantKey(string name)
        {
            var split = name.LastIndexOf('_');
            if (split <= 0 || split >= name.Length - 1)
            {
                return name;
            }

            var suffix = name.Substring(split + 1);
            for (var i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                {
                    return name;
                }
            }

            return name.Substring(0, split);
        }

        static bool IsTooQuiet(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0)
            {
                return true;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if (!clip.LoadAudioData())
            {
                return false;
            }

            var data = new float[Mathf.Min(clip.samples * clip.channels, 48000)];
            if (!clip.GetData(data, 0))
            {
                return false;
            }

            var peak = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var mag = Mathf.Abs(data[i]);
                if (mag > peak)
                {
                    peak = mag;
                }
            }

            return peak < QuietPeak;
#endif
        }

        void BuildFallbacks()
        {
            _fallback["sfx_ui_tap"] = MakeTone(720, 0.05f);
            _fallback["sfx_start"] = MakeChime(440, 660, 0.08f);
            _fallback["sfx_cast_whoosh"] = MakeTone(520, 0.07f);
            _fallback["sfx_splash"] = MakeSplash(0.16f);
            _fallback["sfx_reel"] = MakeTone(340, 0.1f);
            _fallback["sfx_zone_enter"] = MakeTone(400, 0.07f);
            _fallback["sfx_catch_common"] = MakeTone(880, 0.09f);
            _fallback["sfx_catch_rare"] = MakeChime(660, 990, 0.08f);
            _fallback["sfx_catch_mutant"] = MakeChime(520, 1040, 0.1f);
            _fallback["sfx_chip_tick"] = MakeTone(980, 0.05f);
            _fallback["sfx_dive_ready"] = MakeChime(660, 990, 0.08f);
            _fallback["sfx_dive_enter"] = MakeTone(260, 0.14f);
            _fallback["sfx_hazard"] = MakeTone(180, 0.16f);
            _fallback["sfx_mine_hit"] = MakeTone(180, 0.16f);
            _fallback["sfx_oxygen_warn"] = MakeTone(240, 0.18f);
            _fallback["sfx_surface"] = MakeSplash(0.12f);
            _fallback["sfx_timer_tick"] = MakeTone(640, 0.04f);
            _fallback["sfx_time_up"] = MakeTone(160, 0.22f);
            _fallback["sfx_new_best"] = MakeChime(660, 990, 0.08f);
            _fallback["sfx_upgrade_buy"] = MakeChime(520, 780, 0.07f);
            _fallback["sfx_upgrade_denied"] = MakeTone(140, 0.12f);
        }

        AudioSource MakeBgm(string name)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.ignoreListenerPause = true;
            src.priority = 64;
            return src;
        }

        AudioSource MakeSfx(string name)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
            src.priority = 128;
            return src;
        }

        void Crossfade(AudioClip clip, float target)
        {
            if (clip == null)
            {
                return;
            }

            _bgmTarget = target;
            var liveVol = _diving ? target * mix.diveDuck : target;
            if (_bgmLive != null && _bgmLive.clip == clip && _bgmLive.isPlaying)
            {
                FadeLive(liveVol, mix.crossfade);
                return;
            }

            var next = _bgmLive == _bgmA ? _bgmB : _bgmA;
            KillBgm(next);
            KillBgm(_bgmLive);
            next.clip = clip;
            next.loop = true;
            next.volume = 0f;
            next.Play();
            Fade(next, liveVol, mix.crossfade);
            if (_bgmLive != null && _bgmLive.isPlaying && _bgmLive != next)
            {
                var from = _bgmLive;
                Fade(from, 0f, mix.crossfade).OnComplete(() => from.Stop());
            }

            _bgmLive = next;
        }

        void FadeLive(float volume, float dur)
        {
            if (_bgmLive == null)
            {
                return;
            }

            Fade(_bgmLive, volume, dur);
        }

        Tween Fade(AudioSource src, float volume, float dur)
        {
            src.DOKill(false);
            return DOTween.To(() => src.volume, v => src.volume = v, volume, dur)
                .SetTarget(src)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        void KillBgm(AudioSource src)
        {
            if (src != null)
            {
                src.DOKill(false);
            }
        }

        static ScrapGrade GradeOf(ScrapView scrap)
        {
            if (scrap == null)
            {
                return ScrapGrade.Common;
            }

            if (scrap.Definition != null)
            {
                return scrap.Definition.Grade;
            }

            if (scrap.data == null)
            {
                return ScrapGrade.Common;
            }

            switch (scrap.data.Rarity)
            {
                case ItemRarity.Rare:
                    return ScrapGrade.Rare;
                case ItemRarity.Epic:
                case ItemRarity.Legend:
                    return ScrapGrade.Mutant;
                default:
                    return ScrapGrade.Common;
            }
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
            if (_bgmA != null)
            {
                _bgmA.DOKill(false);
            }

            if (_bgmB != null)
            {
                _bgmB.DOKill(false);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
