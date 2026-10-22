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
        const string LibraryResource = "Audio/SoundCueLibrary";
        const string TitleBgmResource = "Audio/Start_Title_BGM";
        const string PlayBgmResource = "Audio/Neo-Tokyo Sludge";
        const int PoolSize = 6;
        const float CountTickGap = 1f / 12f;
        const float LureFullDeniedVolume = 0.3f;
        const string SoundPref = "SoundOn";

        static readonly float[] ZonePitch = { 1f, 0.94f, 0.88f, 0.82f };

        public static AudioManager Instance { get; private set; }

        [SerializeField] AudioMix mix;
        [SerializeField] SoundCueLibrary library;

        readonly Dictionary<SfxId, AudioClip> _procedural = new Dictionary<SfxId, AudioClip>();
        readonly Dictionary<SfxId, int> _frameHits = new Dictionary<SfxId, int>();
        readonly Dictionary<SfxId, float> _readyAt = new Dictionary<SfxId, float>();
        readonly Dictionary<SfxId, AudioClip> _lastClip = new Dictionary<SfxId, AudioClip>();
        readonly Dictionary<int, bool> _quiet = new Dictionary<int, bool>();
        readonly HashSet<SfxId> _missingCue = new HashSet<SfxId>();
        readonly List<AudioClip> _pick = new List<AudioClip>(4);
        AudioSource[] _pool;
        AudioSource _bgmA;
        AudioSource _bgmB;
        AudioSource _bgmLive;
        AudioClip _titleClip;
        AudioClip _playClip;
        int _poolAt;
        int _hitFrame = -1;
        float _bgmTarget = 0.55f;
        float _countTickAt;
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

            if (library == null)
            {
                library = Resources.Load<SoundCueLibrary>(LibraryResource);
            }

            BuildProcedural();
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

            ApplySoundPref();
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
            ResumeBgm();
            if (_wantTitle)
            {
                PlayTitleBgm();
            }
        }

        public bool SoundOn => PlayerPrefs.GetInt(SoundPref, 1) != 0;

        public void SetSoundOn(bool on)
        {
            PlayerPrefs.SetInt(SoundPref, on ? 1 : 0);
            PlayerPrefs.Save();
            ApplySoundPref();
        }

        public void ToggleSound()
        {
            SetSoundOn(!SoundOn);
        }

        void ApplySoundPref()
        {
            AudioListener.volume = SoundOn ? 1f : 0f;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SuspendPlayback();
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                SuspendPlayback();
            }
        }

        void SuspendPlayback()
        {
            _unlocked = false;
            PauseSource(_bgmA);
            PauseSource(_bgmB);
        }

        void ResumeBgm()
        {
            UnpauseSource(_bgmA);
            UnpauseSource(_bgmB);
            if (_bgmLive != null && _bgmLive.clip != null && !_bgmLive.isPlaying)
            {
                _bgmLive.Play();
            }
        }

        static void PauseSource(AudioSource src)
        {
            if (src != null && src.isPlaying)
            {
                src.Pause();
            }
        }

        static void UnpauseSource(AudioSource src)
        {
            if (src != null && src.clip != null)
            {
                src.UnPause();
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
            PlayCue(SfxId.UiTap);
        }

        public void PlayStart()
        {
            PlayCue(SfxId.Start);
        }

        public void PlayCast()
        {
            PlayCue(SfxId.CastWhoosh);
        }

        public void PlaySplash()
        {
            PlayCue(SfxId.Splash);
        }

        public void PlayReel()
        {
            PlayCue(SfxId.Reel);
        }

        public void PlayZone(int index)
        {
            var i = Mathf.Clamp(index, 0, ZonePitch.Length - 1);
            PlayCue(SfxId.ZoneEnter, ZonePitch[i]);
        }

        public void PlayCatch()
        {
            PlayCatchGrade(ScrapGrade.Common);
        }

        public void PlayCatchGrade(ScrapGrade grade)
        {
            if (grade >= ScrapGrade.Mutant)
            {
                if (CanResolve(SfxId.CatchMutant))
                {
                    PlayCue(SfxId.CatchMutant);
                    return;
                }

                PlayCue(SfxId.CatchRare, 0.9f);
                return;
            }

            if (grade >= ScrapGrade.Rare)
            {
                PlayCue(SfxId.CatchRare);
                return;
            }

            PlayCue(SfxId.CatchCommon);
        }

        public void PlayCatch(ScrapView scrap)
        {
            if (scrap != null && scrap.data != null)
            {
                PlayCatchRarity(scrap.data.Rarity);
                return;
            }

            PlayCatchGrade(GradeOf(scrap));
        }

        void PlayCatchRarity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare:
                    PlayCue(SfxId.CatchRare);
                    return;
                case ItemRarity.Epic:
                    PlayCue(SfxId.CatchEpic);
                    return;
                case ItemRarity.Legend:
                // 싱크로 전용 큐가 생기기 전까지 레전드 큐를 쓴다.
                case ItemRarity.Synchro:
                    PlayCue(SfxId.CatchLegend);
                    return;
                default:
                    PlayCue(SfxId.CatchCommon);
                    return;
            }
        }

        public void PlayHazard()
        {
            PlayCue(SfxId.Hazard);
        }

        public void PlayChipArrive(float pitch)
        {
            PlayCue(SfxId.ChipTick, pitch);
        }

        public void PlayDiveReady()
        {
            PlayCue(SfxId.DiveReady);
        }

        public void PlayDive()
        {
            PlayCue(SfxId.DiveEnter);
        }

        public void PlayMineHit()
        {
            PlayCue(SfxId.MineHit);
        }

        public void PlayOxygenWarn()
        {
            PlayCue(SfxId.OxygenWarn);
        }

        public void PlaySurface()
        {
            PlayCue(SfxId.Surface);
        }

        public void PlayTimerTick(float pitch)
        {
            PlayCue(SfxId.TimerTick, pitch);
        }

        public void PlayTimeUp()
        {
            PlayCue(SfxId.TimeUp);
        }

        public void PlayNewBest()
        {
            if (CanResolve(SfxId.NewBest))
            {
                PlayCue(SfxId.NewBest);
                return;
            }

            PlayCue(SfxId.DiveReady, 1.1f);
        }

        public void PlayUpgradeBuy()
        {
            PlayCue(SfxId.UpgradeBuy);
        }

        public void PlayUpgradeDenied()
        {
            PlayCue(SfxId.UpgradeDenied);
        }

        public void PlayMaxedTap()
        {
            PlayCue(SfxId.UiTap, 0.8f);
        }

        public void PlayZoneUnlock()
        {
            PlayCue(SfxId.ZoneUnlock);
        }

        public void PlayUpgradeMax()
        {
            PlayCue(SfxId.UpgradeMax);
        }

        public void PlayLegendAppear()
        {
            PlayCue(SfxId.LegendAppear);
        }

        public void PlayCountTick(float progress)
        {
            if (Time.unscaledTime < _countTickAt)
            {
                return;
            }

            _countTickAt = Time.unscaledTime + CountTickGap;
            var t = Mathf.Clamp01(progress);
            if (HasOwnClip(SfxId.CountTick))
            {
                PlayCue(SfxId.CountTick, Mathf.Lerp(1f, 1.4f, t));
                return;
            }

            if (HasOwnClip(SfxId.ChipTick))
            {
                PlayCue(SfxId.ChipTick, Mathf.Lerp(1f, 1.4f, t));
                return;
            }

            PlayCue(SfxId.UiTap, Mathf.Lerp(1.3f, 1.4f, t));
        }

        public void PlayCountDone()
        {
            PlayCue(SfxId.CountDone);
        }

        public void PlayFull()
        {
            var lure = Cue(SfxId.LureFull);
            if (!ConsumeCooldown(lure))
            {
                return;
            }

            if (HasOwnClip(SfxId.LureFull))
            {
                PlayCue(SfxId.LureFull, 1f, 1f, false, true);
                return;
            }

            var denied = Cue(SfxId.UpgradeDenied);
            var mul = denied != null && denied.volume > 0.0001f ? LureFullDeniedVolume / denied.volume : 1f;
            PlayCue(SfxId.UpgradeDenied, 1f, mul, false, true);
        }

        public void Preview(SfxId id)
        {
            Unlock();
            PlayCue(id, 1f, 1f, false, false);
        }

        public void PlayCue(SfxId id, float pitchMul = 1f, float volumeMul = 1f)
        {
            PlayCue(id, pitchMul, volumeMul, true, true);
        }

        void PlayCue(SfxId id, float pitchMul, float volumeMul, bool checkCooldown, bool checkFrame)
        {
            var cue = Cue(id);
            if (checkCooldown && !ConsumeCooldown(cue))
            {
                return;
            }

            if (checkFrame && !AllowFrame(id, cue != null ? cue.maxPerFrame : 2))
            {
                return;
            }

            var own = false;
            var clip = cue != null ? PickClip(cue) : null;
            var pitchScale = 1f;
            if (clip != null)
            {
                own = true;
            }
            else if (cue != null && cue.fallback != null)
            {
                clip = PickClip(cue.fallback);
                pitchScale = cue.fallbackPitch > 0f ? cue.fallbackPitch : 1f;
            }

            if (clip == null)
            {
                clip = Procedural(id);
                if (cue != null && !own)
                {
                    pitchScale = cue.fallbackPitch > 0f ? cue.fallbackPitch : 1f;
                }
            }

            if (clip == null)
            {
                return;
            }

            var src = NextVoice();
            if (src == null)
            {
                return;
            }

            var volume = (cue != null ? cue.volume : 0.5f) * volumeMul * mix.masterSfx;
            var pitch = Mathf.Clamp(pitchMul, 0.5f, 2f) * (own ? 1f : pitchScale);
            if (cue != null)
            {
                var min = cue.pitchMin;
                var max = cue.pitchMax;
                if (max < min)
                {
                    var swap = min;
                    min = max;
                    max = swap;
                }

                pitch *= Mathf.Approximately(min, max) ? (min > 0f ? min : 1f) : Random.Range(min, max);
            }

            src.Stop();
            src.clip = clip;
            src.pitch = pitch;
            src.volume = volume;
            src.Play();
        }

        SoundCue Cue(SfxId id)
        {
            var cue = library != null ? library.Get(id) : null;
            if (cue == null && _missingCue.Add(id))
            {
                Debug.LogWarning("SoundCue 없음: " + id + ". ScrapFishing/Audio/Build Sound Cues 를 실행하세요.");
            }

            return cue;
        }

        bool HasOwnClip(SfxId id)
        {
            var cue = library != null ? library.Get(id) : null;
            return cue != null && PickClip(cue, false) != null;
        }

        bool CanResolve(SfxId id)
        {
            if (HasOwnClip(id))
            {
                return true;
            }

            var cue = library != null ? library.Get(id) : null;
            return cue != null && cue.fallback != null && HasOwnClip(cue.fallback.id);
        }

        bool ConsumeCooldown(SoundCue cue)
        {
            if (cue == null || cue.cooldown <= 0f)
            {
                return true;
            }

            if (_readyAt.TryGetValue(cue.id, out var at) && Time.unscaledTime < at)
            {
                return false;
            }

            _readyAt[cue.id] = Time.unscaledTime + cue.cooldown;
            return true;
        }

        bool AllowFrame(SfxId id, int maxPerFrame)
        {
            if (Time.frameCount != _hitFrame)
            {
                _hitFrame = Time.frameCount;
                _frameHits.Clear();
            }

            _frameHits.TryGetValue(id, out var hits);
            var limit = maxPerFrame < 1 ? 2 : maxPerFrame;
            if (hits >= limit)
            {
                return false;
            }

            _frameHits[id] = hits + 1;
            return true;
        }

        AudioClip PickClip(SoundCue cue, bool remember = true)
        {
            if (cue == null || cue.clips == null || cue.clips.Length == 0)
            {
                return null;
            }

            _pick.Clear();
            for (var i = 0; i < cue.clips.Length; i++)
            {
                var clip = cue.clips[i];
                if (clip != null && !Quiet(clip))
                {
                    _pick.Add(clip);
                }
            }

            if (_pick.Count == 0)
            {
                return null;
            }

            var index = 0;
            if (_pick.Count > 1)
            {
                index = Random.Range(0, _pick.Count);
                if (_lastClip.TryGetValue(cue.id, out var last) && _pick[index] == last)
                {
                    index = (index + 1) % _pick.Count;
                }
            }

            var choice = _pick[index];
            if (remember)
            {
                _lastClip[cue.id] = choice;
            }

            return choice;
        }

        bool Quiet(AudioClip clip)
        {
            var key = clip.GetInstanceID();
            if (_quiet.TryGetValue(key, out var quiet))
            {
                return quiet;
            }

            quiet = SfxLoudness.IsTooQuiet(clip);
            _quiet[key] = quiet;
            return quiet;
        }

        AudioClip Procedural(SfxId id)
        {
            _procedural.TryGetValue(id, out var clip);
            return clip;
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

        void BuildProcedural()
        {
            _procedural[SfxId.UiTap] = MakeTone(720, 0.05f);
            _procedural[SfxId.Start] = MakeChime(440, 660, 0.08f);
            _procedural[SfxId.CastWhoosh] = MakeTone(520, 0.07f);
            _procedural[SfxId.Splash] = MakeSplash(0.16f);
            _procedural[SfxId.Reel] = MakeTone(340, 0.1f);
            _procedural[SfxId.ZoneEnter] = MakeTone(400, 0.07f);
            _procedural[SfxId.CatchCommon] = MakeTone(880, 0.09f);
            _procedural[SfxId.CatchRare] = MakeChime(660, 990, 0.08f);
            _procedural[SfxId.CatchMutant] = MakeChime(520, 1040, 0.1f);
            _procedural[SfxId.CatchEpic] = MakeChime(520, 1040, 0.1f);
            _procedural[SfxId.CatchLegend] = MakeChime(440, 880, 0.12f);
            _procedural[SfxId.LureFull] = MakeTone(140, 0.12f);
            _procedural[SfxId.ChipTick] = MakeTone(980, 0.05f);
            _procedural[SfxId.DiveReady] = MakeChime(660, 990, 0.08f);
            _procedural[SfxId.DiveEnter] = MakeTone(260, 0.14f);
            _procedural[SfxId.Hazard] = MakeTone(180, 0.16f);
            _procedural[SfxId.MineHit] = MakeTone(180, 0.16f);
            _procedural[SfxId.OxygenWarn] = MakeTone(240, 0.18f);
            _procedural[SfxId.Surface] = MakeSplash(0.12f);
            _procedural[SfxId.TimerTick] = MakeTone(640, 0.04f);
            _procedural[SfxId.TimeUp] = MakeTone(160, 0.22f);
            _procedural[SfxId.NewBest] = MakeChime(660, 990, 0.08f);
            _procedural[SfxId.UpgradeBuy] = MakeChime(520, 780, 0.07f);
            _procedural[SfxId.UpgradeDenied] = MakeTone(140, 0.12f);
            _procedural[SfxId.CountTick] = MakeTone(980, 0.04f);
            _procedural[SfxId.CountDone] = MakeChime(520, 780, 0.07f);
            _procedural[SfxId.ZoneUnlock] = MakeTone(400, 0.08f);
            _procedural[SfxId.UpgradeMax] = MakeChime(660, 990, 0.08f);
            _procedural[SfxId.LegendAppear] = MakeChime(660, 990, 0.08f);
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
                case ItemRarity.Synchro:
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
