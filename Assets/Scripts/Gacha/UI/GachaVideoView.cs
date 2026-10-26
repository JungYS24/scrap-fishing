using System;
using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ScrapFishing.Gacha
{
    // 오리파 뽑기 연출: 머신 영상(StreamingAssets URL) → 등급 섬광 → 흰 화면 위 결과 카드 → 탭하면 닫고 결과 목록으로.
    // 결과는 버튼을 누른 순간 이미 확정·저장되어 있다. 영상을 못 불러오거나 멈추면 연출 없이 바로 끝낸다(onFinish).
    // 영상 머신은 result.Machine이다(데일리·축제는 이번 결과의 최고 등급 장비 머신으로 GachaShop이 정해 둔다).
    // 실행 중에만 만든다(씬에는 두지 않는다). VideoPlayer가 꺼진 오브젝트에서는 영상을 불러오지 못하므로
    // 숨길 때도 오브젝트는 켜 두고 CanvasGroup으로만 감춘다.
    public class GachaVideoView : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            Starting,
            Playing,
            Cards,
        }

        const int VideoWidth = 720;
        const int VideoHeight = 1280;
        const float AudioDriftLimit = 0.12f;

        GachaVideoConfig _config;
        RectTransform _root;
        CanvasGroup _group;
        RectTransform _video;
        RawImage _videoImage;
        Image _flash;
        RectTransform _shards;
        Image _cards;
        RectTransform _grid;
        Text _skip;
        // SKIP 글자 뒤 반투명 어두운 바탕(흰 화면에서도 읽히게). 글자와 같이 켜고 끈다.
        Image _skipBack;
        // 결과 카드 뒤 사이키델릭 파장(흰 바탕 대신). 셰이더를 못 쓰면 null(흰 바탕 그대로).
        PsyWavesBackground _psy;
        Text _hint;
        VideoPlayer _player;
        AudioSource _audio;
        RenderTexture _texture;

        Phase _phase;
        GachaPullResult _result;
        GachaVideoConfig.Entry _entry;
        Action _onFinish;
        string _preparedUrl;
        string _failedUrl;
        bool _started;
        bool _flashed;
        bool _canSkip;
        float _deadline;
        float _revealEnd;
        double _lastVideoTime;
        float _lastProgressAt;

        public bool IsPlaying => _phase != Phase.Idle;
        public bool CanSkip => _canSkip && (_phase == Phase.Starting || _phase == Phase.Playing);
        public bool ShowingCards => _phase == Phase.Cards;
        public bool Revealed => _phase == Phase.Cards && Time.unscaledTime >= _revealEnd;
        public bool Flashed => _flashed;
        public int CardCount => _grid != null ? _grid.childCount : 0;
        public double VideoTime => _player != null ? _player.time : 0d;
        public string PreparedUrl => _preparedUrl;
        public VideoPlayer Player => _player;
        public AudioSource Audio => _audio;
        public RenderTexture Texture => _texture;
        public GachaVideoConfig Config => _config;
        public PsyWavesBackground Background => _psy;
        // 마지막 섬광의 등급(노멀이면 섬광 없이 Normal). 테스트·로그용.
        public ItemRarity LastFlash { get; private set; }

        public static GachaVideoView Create(RectTransform parent, GachaVideoConfig config = null)
        {
            var root = GachaUi.Rect(parent, "GachaVideo", Vector2.zero, Vector2.one);
            var view = root.gameObject.AddComponent<GachaVideoView>();
            view.Build(root, config != null ? config : GachaVideoConfig.Load());
            return view;
        }

        void Build(RectTransform root, GachaVideoConfig config)
        {
            _config = config;
            _root = root;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = Color.black;
            var tap = root.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(HandleTap);

            _texture = new RenderTexture(VideoWidth, VideoHeight, 0) { name = "GachaVideoRT" };
            _video = GachaUi.Rect(root, "Video", Vector2.zero, Vector2.one);
            _videoImage = _video.gameObject.AddComponent<RawImage>();
            _videoImage.texture = _texture;
            _videoImage.raycastTarget = false;
            // 비율을 지키고 남는 부분은 뒤의 검은 바탕이 보인다.
            var fit = _video.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = VideoWidth / (float)VideoHeight;

            _cards = GachaUi.Panel(root, "Cards", Vector2.zero, Vector2.one, Color.white);
            _grid = GachaUi.Rect(_cards.transform, "Grid", new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.92f));
            _hint = GachaUi.Label(_cards.transform, "Hint", 22, TextAnchor.MiddleCenter, new Vector2(0f, 0.02f), new Vector2(1f, 0.08f),
                new Color(0.2f, 0.24f, 0.32f), "탭하여 계속");

            _flash = GachaUi.Panel(root, "Flash", Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f));
            _shards = GachaUi.Rect(root, "Shards", Vector2.zero, Vector2.one);
            // SKIP: 그림 없이 반투명 어두운 바탕(알파 0.55) + 흰 글자, 화면 오른쪽 위.
            // 파장 배경의 밝은 색(싱크로 하양 등) 위에서도 읽히게 바탕 알파를 0.65로.
            _skipBack = GachaUi.Panel(root, "SkipBack", new Vector2(0.74f, 0.94f), new Vector2(0.97f, 0.985f), new Color(0f, 0f, 0f, 0.65f));
            _skip = GachaUi.Label(root, "Skip", 18, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.94f), new Vector2(0.97f, 0.985f),
                Color.white, "SKIP >>");

            // 스킨: "탭하여 계속"은 tap_continue 판 위에 밝은 글자.
            if (OripaLook.Behind(_hint, "tap_continue", new Vector2(0.22f, 0.022f), new Vector2(0.78f, 0.078f)) == null)
            {
                // 스킨이 없어도 파장 배경 위에서 읽히게 어두운 판을 깐다.
                var plate = GachaUi.Panel(_cards.transform, _hint.name + "Plate", new Vector2(0.22f, 0.022f), new Vector2(0.78f, 0.078f), new Color(0f, 0f, 0f, 0.6f));
                plate.transform.SetSiblingIndex(_hint.transform.GetSiblingIndex());
            }

            _hint.color = Palette.Text;
            _psy = PsyWavesBackground.Create((RectTransform)_cards.transform, _config);

            _player = root.gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.Url;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _texture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            // 타이틀·결과 화면은 timeScale이 0이다.
            _player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            _player.errorReceived += HandleError;
            _player.started += HandleStarted;
            _player.loopPointReached += HandleEnded;

            _audio = root.gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.spatialBlend = 0f;
            ApplyVolume();
            VolumeSettings.Changed += ApplyVolume;
            AudioManager.SoundChanged += ApplyVolume;

            HideNow();
        }

        void OnDestroy()
        {
            VolumeSettings.Changed -= ApplyVolume;
            AudioManager.SoundChanged -= ApplyVolume;
            if (_player != null)
            {
                _player.errorReceived -= HandleError;
                _player.started -= HandleStarted;
                _player.loopPointReached -= HandleEnded;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        // 설정의 음소거(전체 AudioListener)와 효과음 음량을 따른다.
        void ApplyVolume()
        {
            if (_audio == null)
            {
                return;
            }

            var audio = AudioManager.Instance;
            _audio.volume = audio != null ? audio.SfxLevel : Mathf.Clamp01(VolumeSettings.SfxGain);
            _audio.mute = audio != null && !audio.SoundOn;
        }

        // 오리파 화면에 들어오거나 머신을 고를 때 그 머신 영상을 미리 불러 둔다.
        public void Prepare(GachaMachineData machine)
        {
            if (machine == null || _phase != Phase.Idle)
            {
                return;
            }

            var entry = _config.For(machine.Id);
            Preload(entry != null ? entry.sfx : null);
            var url = GachaVideoConfig.VideoUrl(entry);
            if (string.IsNullOrEmpty(url) || (url == _preparedUrl && _player.isPrepared))
            {
                return;
            }

            PrepareUrl(url);
        }

        void PrepareUrl(string url)
        {
            _preparedUrl = url;
            if (_failedUrl == url)
            {
                _failedUrl = null;
            }

            _player.Stop();
            _player.url = url;
            // 오리파 화면이 꺼져 있으면 주소만 바꿔 두고, 화면을 열 때(또는 Play가) 다시 불러온다.
            if (isActiveAndEnabled)
            {
                _player.Prepare();
            }
        }

        // 결과가 확정된 뒤 버튼 탭과 같은 프레임에 부른다(브라우저 자동재생 제한을 피한다).
        // 연출을 시작하면 true. false면 연출 없이 호출부가 바로 결과를 보여 준다(onFinish는 부르지 않는다).
        public bool Play(GachaPullResult result, Action onFinish)
        {
            if (result == null || result.Machine == null || result.Count > GachaVideoConfig.MaxVideoPull || _phase != Phase.Idle)
            {
                return false;
            }

            var entry = _config.For(result.Machine.Id);
            var url = GachaVideoConfig.VideoUrl(entry);
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (url == _failedUrl)
            {
                // 이전에 불러오지 못한 영상: 이번엔 건너뛰고 다음 뽑기를 위해 다시 불러 본다.
                PrepareUrl(url);
                return false;
            }

            _entry = entry;
            _result = result;
            _onFinish = onFinish;
            _started = false;
            _flashed = false;
            LastFlash = ItemRarity.Normal;
            _canSkip = GachaVideoConfig.HasSeen(result.Machine.Id);
            if (url != _preparedUrl)
            {
                PrepareUrl(url);
            }

            ShowOverlay();
            _phase = Phase.Starting;
            _deadline = Time.unscaledTime + Mathf.Max(0.5f, _config.startTimeout);
            _audio.Stop();
            _audio.clip = entry.sfx != null ? entry.sfx : Resources.Load<AudioClip>(GachaVideoConfig.SfxPath(entry.machineId));
            ApplyVolume();
            _player.time = 0d;
            _player.Play();
            if (_phase != Phase.Starting)
            {
                // 재생을 누르자마자 오류가 나서 이미 결과로 넘어갔다.
                return true;
            }

            if (_failedUrl == url)
            {
                StopPlayback();
                HideNow();
                return false;
            }

            if (_player.isPrepared)
            {
                // 미리 불러 둔 영상은 같은 순간에 소리도 시작한다.
                StartAudio(0f);
            }

            return true;
        }

        void ShowOverlay()
        {
            _root.SetAsLastSibling();
            _group.DOKill(false);
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            _videoImage.color = Color.white;
            ClearTexture();
            _cards.gameObject.SetActive(false);
            _hint.gameObject.SetActive(false);
            _flash.DOKill(false);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            GachaUi.Clear(_shards);
            GachaUi.Clear(_grid);
            SetSkipVisible(_canSkip);
        }

        // 지난 영상의 마지막 프레임(흰 화면)이 잠깐 보이지 않게 검게 지운다.
        void ClearTexture()
        {
            var active = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = active;
        }

        // WebGL은 소리를 비동기로 풀어 둔다. 아직 안 풀렸으면 이번엔 건너뛰고, 재생 중 매 프레임 영상 시간에 맞춰 다시 시도한다.
        void StartAudio(float at)
        {
            if (_audio.clip == null || _audio.isPlaying)
            {
                return;
            }

            var length = ClipLength();
            if (length <= 0f)
            {
                _audio.clip.LoadAudioData();
                return;
            }

            if (at >= length - 0.1f)
            {
                return;
            }

            _audio.time = Mathf.Clamp(at, 0f, length - 0.05f);
            _audio.Play();
        }

        float ClipLength()
        {
            var clip = _audio.clip;
            return clip != null && clip.loadState == AudioDataLoadState.Loaded ? clip.length : 0f;
        }

        static void Preload(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }

        void HandleStarted(VideoPlayer source)
        {
            if (_phase == Phase.Starting)
            {
                _started = true;
            }
        }

        void HandleError(VideoPlayer source, string message)
        {
            Debug.LogWarning("오리파 연출 영상을 재생하지 못해 결과로 바로 넘어갑니다: " + source.url + " / " + message);
            _failedUrl = source.url;
            if (_phase == Phase.Starting || _phase == Phase.Playing)
            {
                Abort();
            }
        }

        void HandleEnded(VideoPlayer source)
        {
            if (_phase == Phase.Playing || _phase == Phase.Starting)
            {
                ShowCards(true);
            }
        }

        void Update()
        {
            switch (_phase)
            {
                case Phase.Starting:
                    if (_started || (_player.isPlaying && _player.time > 0d))
                    {
                        _phase = Phase.Playing;
                        StartAudio((float)_player.time);
                        _lastVideoTime = _player.time;
                        _lastProgressAt = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime > _deadline)
                    {
                        Debug.LogWarning("오리파 연출 영상이 시작되지 않아 결과로 바로 넘어갑니다: " + _player.url);
                        _failedUrl = _player.url;
                        Abort();
                    }

                    break;
                case Phase.Playing:
                    TickPlaying();
                    break;
            }
        }

        void TickPlaying()
        {
            var time = _player.time;
            if (time > _lastVideoTime + 0.001d)
            {
                _lastVideoTime = time;
                _lastProgressAt = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _lastProgressAt > _config.stallTimeout)
            {
                Debug.LogWarning("오리파 연출 영상이 멈춰 결과로 바로 넘어갑니다: " + _player.url);
                Abort();
                return;
            }

            // 소리가 영상보다 앞서거나 늦으면 영상 시간에 맞춘다.
            if (_audio.isPlaying && Mathf.Abs(_audio.time - (float)time) > AudioDriftLimit)
            {
                _audio.time = Mathf.Clamp((float)time, 0f, Mathf.Max(0f, ClipLength() - 0.05f));
            }
            else if (!_audio.isPlaying)
            {
                StartAudio((float)time);
            }

            if (!_flashed && time >= _entry.flashTime)
            {
                Flash();
            }

            if (time >= _entry.cardTime)
            {
                ShowCards(true);
            }
        }

        void HandleTap()
        {
            switch (_phase)
            {
                case Phase.Starting:
                case Phase.Playing:
                    if (_canSkip)
                    {
                        Skip();
                    }

                    break;
                case Phase.Cards:
                    if (Revealed)
                    {
                        Finish();
                    }

                    break;
            }
        }

        // 본 적 있는 영상: 바로 결과 카드 시점으로. 영상·소리도 그 시점으로 옮긴다.
        public void Skip()
        {
            if (!CanSkip)
            {
                return;
            }

            var at = Mathf.Max(0f, _entry.cardTime);
            if (_player.canSetTime)
            {
                _player.time = at;
            }

            _audio.Stop();
            StartAudio(at);
            if (!_flashed)
            {
                Flash();
            }

            ShowCards(false);
        }

        void Flash()
        {
            _flashed = true;
            var rarity = GachaVideoConfig.HighestRarity(_result);
            LastFlash = rarity;
            var color = GachaVideoConfig.FlashColor(rarity);
            if (!color.HasValue)
            {
                return;
            }

            var duration = Mathf.Max(0.1f, _config.flashDuration);
            var peak = _config.flashAlpha;
            _flash.DOKill(false);
            _flash.transform.SetAsLastSibling();
            SkipToFront();
            if (rarity == ItemRarity.Synchro)
            {
                FlashSynchro(duration, peak);
            }
            else
            {
                var c = color.Value;
                _flash.color = new Color(c.r, c.g, c.b, 0f);
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(_flash.gameObject);
                seq.Append(_flash.DOFade(peak, duration * 0.15f).SetEase(Ease.OutQuad));
                seq.Append(_flash.DOFade(0f, duration * 0.85f).SetEase(Ease.InQuad));
            }

            if (rarity >= ItemRarity.Legend)
            {
                Fx.Shake(_video, _config.legendShakeStrength, _config.legendShakeDuration);
                var sfx = _config.FlashSfx(rarity);
                if (sfx != null)
                {
                    _audio.PlayOneShot(sfx);
                }
            }
        }

        // 싱크로: 하양·핑크·연두가 번갈아 반짝이고, 마름모 결정 조각이 가운데서 퍼진다.
        void FlashSynchro(float duration, float peak)
        {
            var colors = GachaVideoConfig.SynchroFlashColors;
            var pulses = 4;
            var step = duration / pulses;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(_flash.gameObject);
            for (var i = 0; i < pulses; i++)
            {
                var c = colors[i % colors.Length];
                var start = new Color(c.r, c.g, c.b, 0f);
                seq.AppendCallback(() => _flash.color = start);
                seq.Append(_flash.DOFade(peak * (i == 0 ? 1f : 0.75f), step * 0.3f).SetEase(Ease.OutQuad));
                seq.Append(_flash.DOFade(0f, step * 0.7f).SetEase(Ease.InQuad));
            }

            _shards.SetAsLastSibling();
            SkipToFront();
            const int count = 14;
            for (var i = 0; i < count; i++)
            {
                var shard = GachaUi.Panel(_shards, "Shard" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), colors[i % colors.Length]);
                var rect = shard.rectTransform;
                var size = UnityEngine.Random.Range(18f, 42f);
                rect.sizeDelta = new Vector2(size, size * 1.6f);
                var angle = i * Mathf.PI * 2f / count + UnityEngine.Random.Range(-0.2f, 0.2f);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 45f);
                rect.localScale = Vector3.zero;
                var distance = UnityEngine.Random.Range(160f, 320f);
                var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var go = shard.gameObject;
                var life = duration * UnityEngine.Random.Range(1f, 1.4f);
                var shardSeq = DOTween.Sequence().SetUpdate(true).SetLink(go);
                shardSeq.Join(rect.DOAnchorPos(target, life).SetEase(Ease.OutCubic));
                shardSeq.Join(rect.DOScale(1f, life * 0.25f).SetEase(Ease.OutBack));
                shardSeq.Join(shard.DOFade(0f, life * 0.6f).SetDelay(life * 0.4f));
                shardSeq.OnComplete(() => Destroy(go));
            }
        }

        void ShowCards(bool watchedToEnd)
        {
            if (_phase == Phase.Cards)
            {
                return;
            }

            _phase = Phase.Cards;
            if (watchedToEnd)
            {
                GachaVideoConfig.MarkSeen(_result.Machine.Id);
            }

            SetSkipVisible(false);
            _cards.gameObject.SetActive(true);
            _cards.transform.SetSiblingIndex(_video.GetSiblingIndex() + 1);
            BuildCards();
            if (_psy != null)
            {
                _psy.Show(GachaVideoConfig.HighestRarity(_result));
            }
        }

        void BuildCards()
        {
            GachaUi.Clear(_grid);
            var items = _result.Items;
            var count = items.Count;
            var interval = Mathf.Max(0f, _config.cardFlipInterval);
            var flip = Mathf.Max(0.05f, _config.cardFlipDuration);
            if (count == 1 && OripaLook.On)
            {
                // 스킨: 1연도 10연과 같은 가로형 카드(화면 폭 90% = 칸 폭 전체, 비율은 카드 그림 318:218), 가운데.
                var card = BuildCard(items[0].Gear, new Vector2(0f, 0.18f), new Vector2(1f, 0.82f), 30);
                Reveal(card, 0f, flip);
            }
            else if (count == 1)
            {
                var card = BuildCard(items[0].Gear, new Vector2(0.17f, 0.2f), new Vector2(0.83f, 0.8f), 34);
                Reveal(card, 0f, flip);
            }
            else
            {
                var columns = Mathf.Clamp(_config.tenColumns, 1, count);
                var rows = Mathf.CeilToInt(count / (float)columns);
                const float gap = 0.02f;
                var width = (1f - gap * (columns - 1)) / columns;
                var height = (1f - gap * (rows - 1)) / rows;
                for (var i = 0; i < count; i++)
                {
                    var column = i % columns;
                    var row = i / columns;
                    var min = new Vector2(column * (width + gap), 1f - (row + 1) * height - row * gap);
                    var max = new Vector2(min.x + width, min.y + height);
                    var card = BuildCard(items[i].Gear, min, max, 18);
                    Reveal(card, i * interval, flip);
                }
            }

            _revealEnd = Time.unscaledTime + Mathf.Max(0, count - 1) * interval + flip;
            _hint.gameObject.SetActive(false);
            var hintPlate = _hint.transform.parent.Find(_hint.name + "Plate");
            if (hintPlate != null)
            {
                hintPlate.gameObject.SetActive(false);
            }
            DOVirtual.DelayedCall(_revealEnd - Time.unscaledTime, ShowHint, true).SetLink(_grid.gameObject);
        }

        void SetSkipVisible(bool visible)
        {
            _skip.gameObject.SetActive(visible);
            _skipBack.gameObject.SetActive(visible);
        }

        void SkipToFront()
        {
            _skipBack.transform.SetAsLastSibling();
            _skip.transform.SetAsLastSibling();
        }

        void ShowHint()
        {
            if (_phase != Phase.Cards)
            {
                return;
            }

            _hint.gameObject.SetActive(true);
            var plate = _hint.transform.parent.Find(_hint.name + "Plate");
            if (plate != null)
            {
                plate.gameObject.SetActive(true);
            }

            _hint.DOKill(false);
            _hint.color = new Color(_hint.color.r, _hint.color.g, _hint.color.b, 1f);
            _hint.DOFade(0.35f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_hint.gameObject);
        }

        // 테두리는 등급 색(싱크로는 하양·핑크·연두로 번갈아), 안쪽은 어두운 카드에 장비 아이콘·이름·등급.
        // 스킨이면 result_card_{등급}(아래 이름판 포함)을 칸 안에 원래 비율로 넣고, 아이콘 뒤에 등급색 빛·레전드 이상은 반짝이.
        RectTransform BuildCard(GearData gear, Vector2 min, Vector2 max, int nameSize)
        {
            var rarity = gear != null ? gear.Rarity : ItemRarity.Normal;
            var color = GachaUi.RarityColor(rarity);
            var skin = OripaLook.Get(OripaLook.Of("result_card", rarity));
            if (skin != null)
            {
                return BuildSkinCard(gear, rarity, color, skin, min, max, nameSize);
            }

            var border = GachaUi.Panel(_grid, "Card_" + (gear != null ? gear.Id : "?"), min, max, color);
            var rect = border.rectTransform;
            var inner = GachaUi.Panel(rect, "Inner", Vector2.zero, Vector2.one, GachaUi.Card).rectTransform;
            inner.offsetMin = new Vector2(4f, 4f);
            inner.offsetMax = new Vector2(-4f, -4f);

            var art = GachaUi.Rect(inner, "Art", new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.95f));
            if (gear != null && gear.Icon != null)
            {
                var icon = art.gameObject.AddComponent<Image>();
                icon.sprite = gear.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            else
            {
                GachaUi.Panel(art, "Swatch", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), color);
            }

            var name = GachaUi.Label(inner, "Name", nameSize, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.3f), Palette.Text,
                gear != null ? gear.DisplayName : "?");
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 10;
            name.resizeTextMaxSize = nameSize;
            GachaUi.Label(inner, "Rarity", Mathf.Max(12, nameSize - 6), TextAnchor.MiddleCenter, new Vector2(0f, 0.01f), new Vector2(1f, 0.13f), color,
                GachaUi.RarityLabel(rarity));

            if (rarity == ItemRarity.Synchro)
            {
                var colors = GachaVideoConfig.SynchroFlashColors;
                var cycle = DOTween.Sequence().SetLoops(-1).SetUpdate(true).SetLink(border.gameObject);
                for (var i = 0; i < colors.Length; i++)
                {
                    cycle.Append(border.DOColor(colors[(i + 1) % colors.Length], 0.4f).SetEase(Ease.InOutSine));
                }
            }

            return rect;
        }

        RectTransform BuildSkinCard(GearData gear, ItemRarity rarity, Color color, Sprite skin, Vector2 min, Vector2 max, int nameSize)
        {
            // 칸(cell) 안에 카드 그림 비율 그대로 맞춘다. 뒤집기 연출은 카드에 건다.
            var cell = GachaUi.Rect(_grid, "Cell_" + (gear != null ? gear.Id : "?"), min, max);
            var card = GachaUi.Panel(cell, "Card", Vector2.zero, Vector2.one, Color.white);
            var fit = card.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = skin.rect.width / skin.rect.height;
            OripaLook.Apply(card, OripaLook.Of("result_card", rarity));
            var rect = card.rectTransform;

            // 아이콘: 카드 높이의 50%, 이름판 위 가운데. 뒤에 등급색 원형 빛.
            OripaLook.Glow(rect, new Vector2(0.22f, 0.3f), new Vector2(0.78f, 1f), color);
            var art = GachaUi.Rect(rect, "Art", new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.9f));
            if (gear != null && gear.Icon != null)
            {
                var icon = art.gameObject.AddComponent<Image>();
                icon.sprite = gear.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            else
            {
                GachaUi.Panel(art, "Swatch", new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.9f), color);
            }

            if (rarity >= ItemRarity.Legend)
            {
                OripaLook.Sparkles(rect, rarity == ItemRarity.Synchro ? Color.white : new Color(1f, 0.85f, 0.5f), rarity == ItemRarity.Synchro ? 7 : 5);
            }

            // 이름판: 왼쪽 점 옆에 이름, 오른쪽에 등급 글자(작게).
            // 이름판 안에서 이름(왼쪽)과 등급(오른쪽)이 겹치지 않게 칸을 나누고, 각자 한 줄로 줄여 넣는다.
            var name = GachaUi.Label(rect, "Name", nameSize, TextAnchor.MiddleLeft, new Vector2(0.17f, 0.055f), new Vector2(0.68f, 0.2f), Palette.Text,
                gear != null ? gear.DisplayName : "?");
            TextOneLine.Fit(name, nameSize, 9);
            var label = GachaUi.Label(rect, "Rarity", Mathf.Max(11, nameSize / 2), TextAnchor.MiddleRight, new Vector2(0.69f, 0.055f), new Vector2(0.87f, 0.2f), color,
                GachaUi.RarityLabel(rarity));
            TextOneLine.Fit(label, Mathf.Max(11, nameSize / 2), 8);
            return rect;
        }

        // 카드 뒤집기: 가로 크기 0에서 펼쳐지며 나타난다.
        static void Reveal(RectTransform card, float delay, float duration)
        {
            card.localScale = new Vector3(0f, 1f, 1f);
            card.DOScaleX(1f, duration).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true).SetLink(card.gameObject);
        }

        // 결과 카드를 본 뒤 탭: 남은 흰 화면은 기다리지 않고 닫는다.
        public void Finish()
        {
            if (_phase != Phase.Cards)
            {
                return;
            }

            var finish = _onFinish;
            StopPlayback();
            _group.blocksRaycasts = false;
            _group.DOKill(false);
            _group.DOFade(0f, 0.2f).SetUpdate(true).SetLink(_root.gameObject).OnComplete(HideNow);
            finish?.Invoke();
        }

        // 영상 오류·시간 초과: 연출 없이 바로 결과로.
        void Abort()
        {
            var finish = _onFinish;
            StopPlayback();
            HideNow();
            finish?.Invoke();
        }

        void StopPlayback()
        {
            _phase = Phase.Idle;
            _onFinish = null;
            _audio.Stop();
            _player.Stop();
            Fx.Restore(_video);
            // 다음 뽑기를 위해 같은 영상을 다시 불러 둔다(불러오지 못한 영상은 다음 Prepare 때 다시 시도).
            if (!string.IsNullOrEmpty(_preparedUrl) && _preparedUrl != _failedUrl)
            {
                _player.url = _preparedUrl;
                if (isActiveAndEnabled)
                {
                    _player.Prepare();
                }
            }
        }

        public void HideNow()
        {
            if (_phase != Phase.Idle)
            {
                StopPlayback();
            }

            _group.DOKill(false);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _cards.gameObject.SetActive(false);
            if (_psy != null)
            {
                _psy.Hide();
            }

            SetSkipVisible(false);
        }
    }
}
