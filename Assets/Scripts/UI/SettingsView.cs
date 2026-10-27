using System;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.FX;
using ScrapFishing.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 설정창: 음량 3종(전체·배경음·효과음), 기본값, 언어(한↔영 즉시 전환), 크레딧, 버전.
    // 타이틀·결과 화면과 플레이 중 HUD(보트·잠수)의 톱니 버튼으로 연다. 겉모습은 Resources/UI/SettingsSkin 스프라이트(9-slice)로 그리고,
    // 스킨이 없으면 오리파 화면(GachaUi)과 같은 단색 톤으로 그린다.
    // 배치 값은 스킨 시안(560×820)의 픽셀 위치를 패널 비율로 옮긴 것이다(패널 크기·위치는 그대로).
    public class SettingsView : MonoBehaviour
    {
        const float ArtWidth = 560f;
        const float ArtHeight = 820f;
        // 음량 한 줄: 위쪽 이름+수치 칸, 아래쪽 슬라이더. 슬라이더 칸은 줄 아래로 조금 넘쳐 터치 영역을 넓힌다(약 44px).
        const float LabelBottom = 0.564f;
        // 이름+수치 칸 폭(줄 폭 비율). "전체 음량  100"이 20px 글자로 들어가는 폭이다.
        const float LabelRight = 0.48f;
        const float SliderBottom = -0.2f;
        // 슬라이더 칸 안의 트랙 위치. 손잡이는 트랙 높이의 1.6배로 보인다(손잡이 크기는 여기서만 정한다).
        const float TrackMin = 0.396f;
        const float TrackMax = 0.732f;
        public const float KnobTrackRatio = 1.6f;
        const float KnobAspect = 46f / 44f;
        // 손잡이 반폭(px). 트랙·채움이 손잡이 중심까지 오도록 양옆을 이만큼 비운다.
        const float KnobPad = 13f;
        const float PressedTint = 0.8f;
        const float MutedRowAlpha = 0.4f;
        const float OpenDuration = 0.2f;
        const float CloseDuration = 0.15f;
        const string GearIconResource = "UI/icon_settings";

        static readonly VolumeChannel[] Channels = { VolumeChannel.Master, VolumeChannel.Bgm, VolumeChannel.Sfx };
        static readonly string[] ChannelKeys = { "ui.settings.vol.master", "ui.settings.vol.bgm", "ui.settings.vol.sfx" };

        // 크레딧 항목(제목 키, 내용 키 또는 번역하지 않는 고유 이름). 내용이 빈 항목은 화면에 나오지 않는다(출처를 확인하면 채운다).
        static readonly string[,] Credits =
        {
            { "ui.credits.made_by", "ui.credits.made_by_body" },
            { "ui.credits.engine", "Unity 6" },
            { "ui.credits.tween", "DOTween Pro (Demigiant)" },
            { "ui.credits.font", "ui.credits.font_body" },
            { "ui.credits.art", "ui.credits.art_body" },
            { "ui.credits.dive_bg", "ui.credits.dive_bg_body" },
            { "ui.credits.sfx", "ui.credits.sfx_body" },
            { "ui.credits.play_bgm", "" },
            { "ui.credits.ui_art", "" }
        };

        static Sprite _gear;

        RectTransform _root;
        CanvasGroup _group;
        RectTransform _panel;
        // 쿠폰 코드(대소문자·앞뒤 공백 무시)와 받는 달러. 몇 번이든 쓸 수 있다.
        public const string CouponCode = "smtm";
        public const int CouponDollars = 9999999;

        RectTransform _coupon;
        CanvasGroup _couponGroup;
        RectTransform _couponPanel;
        InputField _couponInput;
        Text _couponMessage;
        // 오리파 스킨이 있으면 쿠폰 입력칸이 틀린 코드에 빨간 칸(input_field_error)으로 바뀐다.
        bool _couponSkinned;
        RectTransform _credits;
        CanvasGroup _creditsGroup;
        RectTransform _creditsPanel;
        ScrollRect _creditsScroll;
        Text _toast;
        readonly Slider[] _sliders = new Slider[3];
        readonly Text[] _values = new Text[3];
        readonly CanvasGroup[] _rows = new CanvasGroup[3];
        Text _volumeHeader;
        SettingsSkin _skin;
        bool _syncing;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        // 닫기를 누른 순간(사라지는 연출 전) 알린다. 플레이 중 열었다면 여기서 게임을 다시 움직인다.
        public event Action Closed;
        bool Skinned => _skin != null;

        public void Build(RectTransform area)
        {
            _skin = SettingsSkin.Load();
            if (_skin != null && !_skin.Complete)
            {
                Debug.LogWarning("설정 패널 스킨에 빠진 스프라이트가 있어 단색으로 그립니다: Resources/" + SettingsSkin.Resource);
                _skin = null;
            }

            _root = GachaUi.Rect(area, "Settings", Vector2.zero, Vector2.one);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            Clickable(GachaUi.Panel(_root, "Dim", Vector2.zero, Vector2.one, GachaUi.Dim, null, true), Close);

            var panel = Skinned
                ? GachaUi.Panel(_root, "Panel", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f), Color.white, null, true)
                : GachaUi.Panel(_root, "Panel", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f), GachaUi.Card, Palette.NeonGreen, true);
            _panel = panel.rectTransform;
            if (Skinned)
            {
                SlicedFit.Apply(panel, _skin.panel);
            }

            BuildHeader();
            _volumeHeader = GachaUi.Label(_panel, "VolumeHeader", 16, TextAnchor.MiddleLeft, Art(58f, 165f), Art(436f, 125f), GachaUi.Muted, Loc.T("ui.settings.volume"));
            BuildSoundButton(Art(452f, 177f), Art(516f, 117f));
            for (var i = 0; i < Channels.Length; i++)
            {
                var top = 172f + i * 102f;
                BuildRow(i, Art(56f, top + 78f), Art(504f, top));
            }

            WideButton("Defaults", "ui.settings.reset", 478f, 534f, Palette.Cyan, Skinned ? _skin.wideCyan : null, ResetDefaults);
            // 누르면 창 없이 바로 한↔영 전환. 버튼 글자도 바뀐 언어로("Language: English") 다시 쓰인다.
            WideButton("Language", "ui.settings.language", 543f, 600f, GachaUi.Muted, Skinned ? _skin.wideGray : null, Loc.Toggle);
            WideButton("Credits", "ui.settings.credits", 610f, 667f, Palette.Gold, Skinned ? _skin.wideYellow : null, OpenCredits);
            WideButton("Coupon", "ui.settings.coupon", 676f, 733f, Palette.NeonGreen, Skinned ? _skin.wideGreen : null, OpenCoupon);
            BuildFooter();

            var toast = GachaUi.Panel(_panel, "Toast", new Vector2(0.12f, 0.085f), new Vector2(0.88f, 0.16f), GachaUi.Inset, Palette.NeonGreen);
            _toast = GachaUi.Label(toast.transform, "Text", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Palette.NeonGreen);
            toast.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            toast.gameObject.SetActive(false);

            BuildCredits();
            BuildCoupon();
            VolumeSettings.Changed += Sync;
            AudioManager.SoundChanged += Sync;
            Loc.Changed += Sync;
            _root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            VolumeSettings.Changed -= Sync;
            AudioManager.SoundChanged -= Sync;
            Loc.Changed -= Sync;
        }

        public void Open()
        {
            if (_root == null || IsOpen)
            {
                return;
            }

            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _credits.gameObject.SetActive(false);
            _coupon.gameObject.SetActive(false);
            Sync();
            ShowOverlay(_group, _panel);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            VolumeSettings.Save();
            HideOverlay(_group, _panel, () => _root.gameObject.SetActive(false));
            Closed?.Invoke();
        }

        // 톱니 아이콘(Resources/UI/icon_settings) 자체가 버튼이다. 테두리 없이 이미지만 그린다.
        public static Button CreateGearButton(RectTransform parent, Vector2 min, Vector2 max, Action onClick)
        {
            var rect = GachaUi.Rect(parent, "SettingsButton", min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = GearSprite();
            image.color = Color.white;
            image.preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            button.onClick.AddListener(() =>
            {
                AudioManager.Ensure().PlayUiTap();
                onClick?.Invoke();
            });
            return button;
        }

        // 스킨 시안(560×820)의 픽셀 위치(위 기준) → 패널 앵커(아래 기준).
        static Vector2 Art(float x, float y)
        {
            return new Vector2(x / ArtWidth, 1f - y / ArtHeight);
        }

        // 제목 바("설정" 왼쪽 정렬)와 닫기 버튼. 닫기·음소거는 오른쪽 위에 같은 크기로 세로로 놓는다.
        void BuildHeader()
        {
            var min = Art(58f, 104f);
            var max = Art(442f, 52f);
            var closeMin = Art(452f, 110f);
            var closeMax = Art(516f, 50f);
            if (Skinned)
            {
                var bar = GachaUi.Panel(_panel, "Header", min, max, Color.white);
                SlicedFit.Apply(bar, _skin.headerBar);
                LocText.Bind(GachaUi.Label(bar.transform, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0.06f, 0f), new Vector2(0.96f, 1f), Palette.Text),
                    "ui.settings.title");
                IconButton("Close", closeMin, closeMax, _skin.close, Close, true);
                return;
            }

            LocText.Bind(GachaUi.Label(_panel, "Title", 28, TextAnchor.MiddleLeft, min, max, Palette.Text), "ui.settings.title");
            GachaUi.MakeButton(_panel, "Close", "X", closeMin, closeMax, Palette.Magenta, 28, Close);
        }

        void BuildSoundButton(Vector2 min, Vector2 max)
        {
            if (!Skinned)
            {
                SoundToggle.Create(_panel, min, max);
                return;
            }

            // HUD 음소거 버튼과 같은 이름(SoundToggle)으로 두고, 글자 대신 스피커 그림만 바꾼다.
            var button = IconButton("SoundToggle", min, max, _skin.soundOn, () => AudioManager.Ensure().ToggleSound(), false);
            button.gameObject.AddComponent<SettingsSoundButton>().Bind((Image)button.targetGraphic, _skin.soundOn, _skin.soundOff);
        }

        Button IconButton(string name, Vector2 min, Vector2 max, Sprite sprite, Action onClick, bool tapSound)
        {
            var rect = GachaUi.Rect(_panel, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            PressTint(button);
            button.onClick.AddListener(() =>
            {
                if (tapSound)
                {
                    AudioManager.Ensure().PlayUiTap();
                }

                onClick?.Invoke();
            });
            return button;
        }

        // 넓은 버튼 4개: 스킨이면 9-slice 그림을 깔고 기존 테두리 선은 끈다. 글자 색은 버튼 테두리 색(accent).
        void WideButton(string name, string key, float top, float bottom, Color accent, Sprite sprite, Action onClick)
        {
            var button = LocText.Bind(GachaUi.MakeButton(_panel, name, string.Empty, Art(55f, bottom), Art(505f, top), accent, 22, onClick), key);
            if (sprite == null)
            {
                return;
            }

            SlicedFit.Apply((Image)button.targetGraphic, sprite);
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            PressTint(button);
            var text = GachaUi.LabelOf(button);
            if (text != null)
            {
                // 다른 언어로 글자가 길어져도 칸 안에 들어가게 줄인다.
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = 22;
                text.rectTransform.offsetMin = new Vector2(16f, 0f);
                text.rectTransform.offsetMax = new Vector2(-16f, 0f);
            }
        }

        void BuildFooter()
        {
            var min = Art(202f, 781f);
            var max = Art(357f, 746f);
            if (Skinned)
            {
                var box = GachaUi.Panel(_panel, "Footer", min, max, Color.white);
                SlicedFit.Apply(box, _skin.footerBox);
                LocText.Bind(GachaUi.Label(box.transform, "Version", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, GachaUi.Muted), () => TitleView.VersionLabel);
                return;
            }

            LocText.Bind(GachaUi.Label(_panel, "Version", 16, TextAnchor.MiddleCenter, new Vector2(0.05f, min.y), new Vector2(0.95f, max.y), GachaUi.Muted),
                () => TitleView.VersionLabel);
        }

        // 윗줄은 이름과 수치를 한 칸에("전체 음량  40", 수치는 초록), 아랫줄은 손가락으로 끌기 쉬운 넓은 슬라이더.
        void BuildRow(int index, Vector2 min, Vector2 max)
        {
            var channel = Channels[index];
            var row = GachaUi.Rect(_panel, "Row_" + channel, min, max);
            _rows[index] = row.gameObject.AddComponent<CanvasGroup>();
            // 이름+수치 칸은 배경 그림 없이 글자만 둔다(label_box는 쓰지 않는다).
            var box = GachaUi.Rect(row, "LabelBox", new Vector2(0f, LabelBottom), new Vector2(LabelRight, 1f));
            var value = GachaUi.Label(box.transform, "Value", 20, TextAnchor.MiddleLeft, new Vector2(0.07f, 0f), new Vector2(0.97f, 1f), Palette.Text);
            value.supportRichText = true;
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 14;
            value.resizeTextMaxSize = 20;
            _values[index] = value;

            var slider = CreateSlider(row, new Vector2(0f, SliderBottom), new Vector2(1f, LabelBottom), _skin);
            slider.onValueChanged.AddListener(v =>
            {
                if (!_syncing)
                {
                    VolumeSettings.Set(channel, Mathf.RoundToInt(v), false);
                }
            });

            // 손을 뗄 때 저장하고, 전체·효과음은 탭 효과음으로 바뀐 크기를 들려준다. 배경음은 재생 중인 BGM으로 바로 들린다.
            slider.gameObject.AddComponent<UiPointerRelease>().Released += () =>
            {
                VolumeSettings.Save();
                if (channel != VolumeChannel.Bgm)
                {
                    AudioManager.Ensure().PlayUiTap();
                }
            };
            _sliders[index] = slider;
        }

        // 트랙·채움은 9-slice, 손잡이는 육각 너트(Simple). 손잡이 높이는 트랙 높이 × KnobTrackRatio로 앵커가 정하고
        // 폭은 원본 비율을 따른다. 터치는 슬라이더 칸 전체(투명 히트 영역)로 받는다.
        static Slider CreateSlider(RectTransform parent, Vector2 min, Vector2 max, SettingsSkin skin)
        {
            var root = GachaUi.Rect(parent, "Slider", min, max);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var track = skin != null
                ? GachaUi.Panel(root, "Track", new Vector2(0f, TrackMin), new Vector2(1f, TrackMax), Color.white)
                : GachaUi.Panel(root, "Track", new Vector2(0f, TrackMin), new Vector2(1f, TrackMax), GachaUi.Inset, GachaUi.Muted);
            track.rectTransform.offsetMin = new Vector2(KnobPad, 0f);
            track.rectTransform.offsetMax = new Vector2(-KnobPad, 0f);
            if (skin != null)
            {
                SlicedFit.Apply(track, skin.sliderTrack);
            }

            // 채움은 트랙 홈 안쪽에서 늘어난다(원본 22px 트랙 안 12px 채움).
            var fillArea = GachaUi.Rect(track.rectTransform, "FillArea", new Vector2(0f, 0.23f), new Vector2(1f, 0.77f));
            fillArea.offsetMin = new Vector2(3f, 0f);
            fillArea.offsetMax = new Vector2(-3f, 0f);
            var fill = GachaUi.Panel(fillArea, "Fill", Vector2.zero, Vector2.one, Palette.NeonGreen);
            if (skin != null)
            {
                SlicedFit.Apply(fill, skin.sliderFill);
            }

            // Slider가 손잡이의 세로 앵커를 0~1로 덮어쓰므로, 손잡이 높이는 손잡이 영역(HandleArea)의 세로 범위로 정한다.
            var half = (TrackMax - TrackMin) * KnobTrackRatio * 0.5f;
            var center = (TrackMin + TrackMax) * 0.5f;
            var handleArea = GachaUi.Rect(root, "HandleArea", new Vector2(0f, center - half), new Vector2(1f, center + half));
            handleArea.offsetMin = new Vector2(KnobPad, 0f);
            handleArea.offsetMax = new Vector2(-KnobPad, 0f);
            var handleMin = Vector2.zero;
            var handleMax = new Vector2(0f, 1f);
            var handle = skin != null
                ? GachaUi.Panel(handleArea, "Handle", handleMin, handleMax, Color.white, null, true)
                : GachaUi.Panel(handleArea, "Handle", handleMin, handleMax, Palette.Text, Palette.NeonGreen, true);
            if (skin != null)
            {
                handle.sprite = skin.sliderKnob;
                handle.preserveAspect = true;
            }

            var fitter = handle.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = skin != null ? KnobAspect : 1f;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = VolumeSettings.MaxValue;
            slider.wholeNumbers = true;
            PressTint(slider);
            // 0이면 채움을 숨긴다(폭 0인 9-slice의 왼쪽 모서리가 깨지지 않게).
            slider.onValueChanged.AddListener(v => fill.enabled = v > 0f);
            return slider;
        }

        // Color Tint로 두고 눌렀을 때만 약간 어둡게(약 0.8).
        static void PressTint(Selectable selectable)
        {
            selectable.transition = Selectable.Transition.ColorTint;
            var colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(PressedTint, PressedTint, PressedTint, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            selectable.colors = colors;
        }

        // "전체 음량  40": 이름 바로 옆에 수치를 초록으로 붙인다.
        public static string VolumeLabel(int index, int value)
        {
            return Loc.T(ChannelKeys[index]) + "  <color=" + GachaUi.Hex(Palette.NeonGreen) + ">" + value + "</color>";
        }

        // 음소거는 슬라이더 값과 별개로 동작한다. 음소거 중이면 값은 그대로 두고 줄을 흐리게, 머리글에 표시한다.
        void Sync()
        {
            var muted = !AudioManager.Ensure().SoundOn;
            if (_volumeHeader != null)
            {
                _volumeHeader.text = Loc.T(muted ? "ui.settings.volume_muted" : "ui.settings.volume");
                _volumeHeader.color = muted ? Palette.Magenta : GachaUi.Muted;
            }

            for (var i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] != null)
                {
                    _rows[i].alpha = muted ? MutedRowAlpha : 1f;
                }
            }

            _syncing = true;
            for (var i = 0; i < Channels.Length; i++)
            {
                var value = VolumeSettings.Get(Channels[i]);
                if (_sliders[i] != null)
                {
                    _sliders[i].SetValueWithoutNotify(value);
                    var fill = _sliders[i].fillRect != null ? _sliders[i].fillRect.GetComponent<Image>() : null;
                    if (fill != null)
                    {
                        fill.enabled = value > 0;
                    }
                }

                if (_values[i] != null)
                {
                    _values[i].text = VolumeLabel(i, value);
                }
            }

            _syncing = false;
        }

        void ResetDefaults()
        {
            VolumeSettings.ResetDefaults();
            Sync();
            ShowToast(Loc.T("ui.settings.reset_done"));
        }

        void BuildCredits()
        {
            _credits = GachaUi.Rect(_root, "CreditsOverlay", Vector2.zero, Vector2.one);
            _creditsGroup = _credits.gameObject.AddComponent<CanvasGroup>();
            Clickable(GachaUi.Panel(_credits, "Dim", Vector2.zero, Vector2.one, GachaUi.Dim, null, true), CloseCredits);

            var creditsImage = GachaUi.Panel(_credits, "Panel", new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f), GachaUi.Card, Palette.Gold, true);
            _creditsPanel = creditsImage.rectTransform;
            var creditsTitle = GachaUi.Label(_creditsPanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), Palette.Gold);
            LocText.Bind(creditsTitle, "ui.settings.credits");
            // 오리파 스킨: popup_gold_tall(제목은 노란 바 위 어두운 글자), 항목 제목은 section_label 칸 안, 오른쪽 스크롤 막대, 닫기 btn_gray_flat.
            var skinned = OripaLook.Apply(creditsImage, "popup_gold_tall");
            if (skinned)
            {
                GachaUi.Stretch(creditsTitle.rectTransform, new Vector2(0.1f, 0.895f), new Vector2(0.9f, 0.975f));
                creditsTitle.color = Palette.Gold;
            }

            var list = GachaUi.Scroll(_creditsPanel, "List", new Vector2(0.06f, 0.14f), new Vector2(skinned ? 0.9f : 0.96f, 0.875f), 12f, out _creditsScroll);
            for (var i = 0; i < Credits.GetLength(0); i++)
            {
                var head = Credits[i, 0];
                var body = Credits[i, 1];
                var plain = skinned;
                if (string.IsNullOrEmpty(body))
                {
                    continue;
                }

                if (skinned)
                {
                    var row = GachaUi.Rect(list, "Head" + i, Vector2.zero, Vector2.one);
                    GachaUi.Height(row, 30f);
                    var plate = OripaLook.Layer(row, "Label", "section_label", new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.96f));
                    var headText = GachaUi.Label(plate.transform, "Text", 16, TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
                        new Color(0.03f, 0.12f, 0.06f, 1f));
                    TextOneLine.Fit(headText, 16, 10);
                    LocText.Bind(headText, head);
                }

                var entry = GachaUi.Label(list, "Credit" + i, 22, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, Palette.Text);
                LocText.Bind(entry, () => plain
                    ? CreditText(body)
                    : "<color=" + GachaUi.Hex(Palette.NeonGreen) + "><size=16>" + Loc.T(head) + "</size></color>\n" + CreditText(body));
                entry.horizontalOverflow = HorizontalWrapMode.Wrap;
                entry.verticalOverflow = VerticalWrapMode.Overflow;
                entry.lineSpacing = 1.1f;
            }

            var closeCredits = LocText.Bind(GachaUi.MakeButton(_creditsPanel, "Close", string.Empty, new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.11f), GachaUi.Muted, 22,
                CloseCredits), "ui.common.close");
            if (skinned)
            {
                OripaLook.Button(closeCredits, "btn_gray_flat", new Color(0.08f, 0.09f, 0.12f, 1f));
                AddScrollbar(_creditsPanel, _creditsScroll, new Vector2(0.915f, 0.15f), new Vector2(0.945f, 0.875f));
            }
            _credits.gameObject.SetActive(false);
        }

        // 내용 칸: 번역 키면 번역, 아니면("Unity 6" 같은 고유 이름) 그대로.
        static string CreditText(string body)
        {
            return Loc.Or(body, body);
        }

        // 스크롤 막대: scroll_track 안에서 scroll_thumb가 움직인다.
        static void AddScrollbar(RectTransform parent, ScrollRect scroll, Vector2 min, Vector2 max)
        {
            var track = OripaLook.Layer(parent, "Scrollbar", "scroll_track", min, max);
            if (track == null)
            {
                return;
            }

            track.raycastTarget = true;
            var area = GachaUi.Rect(track.transform, "SlidingArea", Vector2.zero, Vector2.one);
            area.offsetMin = new Vector2(0f, 4f);
            area.offsetMax = new Vector2(0f, -4f);
            var thumb = OripaLook.Layer(area, "Thumb", "scroll_thumb", new Vector2(0.25f, 0f), new Vector2(0.75f, 1f));
            thumb.raycastTarget = true;
            thumb.preserveAspect = false;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = thumb.rectTransform;
            bar.targetGraphic = thumb;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        void OpenCredits()
        {
            _credits.SetAsLastSibling();
            _credits.gameObject.SetActive(true);
            _creditsScroll.verticalNormalizedPosition = 1f;
            ShowOverlay(_creditsGroup, _creditsPanel);
        }

        void CloseCredits()
        {
            if (!_credits.gameObject.activeSelf)
            {
                return;
            }

            HideOverlay(_creditsGroup, _creditsPanel, () => _credits.gameObject.SetActive(false));
        }

        // 쿠폰 입력 창: 코드 입력칸, 안내 글자, 취소·확인. 바깥을 눌러도 닫힌다.
        void BuildCoupon()
        {
            _coupon = GachaUi.Rect(_root, "CouponOverlay", Vector2.zero, Vector2.one);
            _couponGroup = _coupon.gameObject.AddComponent<CanvasGroup>();
            Clickable(GachaUi.Panel(_coupon, "Dim", Vector2.zero, Vector2.one, GachaUi.Dim, null, true), CloseCoupon);

            // 모바일 키보드가 화면 아래 40~45%를 덮어도 입력칸·안내·버튼이 다 보이게 창을 화면 위쪽 절반에 둔다.
            _couponPanel = GachaUi.Panel(_coupon, "Panel", new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.86f), GachaUi.Card, Palette.NeonGreen, true).rectTransform;
            LocText.Bind(GachaUi.Label(_couponPanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f), Palette.NeonGreen),
                "ui.coupon.title");
            _couponInput = CreateInput(_couponPanel, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.74f));
            _couponMessage = GachaUi.Label(_couponPanel, "Message", 16, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.48f), GachaUi.Muted,
                Loc.T("ui.coupon.hint"));
            var cancel = LocText.Bind(GachaUi.MakeButton(_couponPanel, "Cancel", string.Empty, new Vector2(0.08f, 0.07f), new Vector2(0.46f, 0.3f), GachaUi.Muted, 22,
                CloseCoupon), "ui.common.cancel");
            var confirm = LocText.Bind(GachaUi.MakeButton(_couponPanel, "Confirm", string.Empty, new Vector2(0.54f, 0.07f), new Vector2(0.92f, 0.3f), Palette.NeonGreen, 22,
                RedeemCoupon), "ui.common.confirm");
            // 오리파 스킨: popup_green(제목은 초록 바 위 어두운 글자), 입력칸 input_field, 버튼 btn_outline_gray / btn_outline_green.
            _couponSkinned = OripaLook.Apply(_couponPanel.GetComponent<Image>(), "popup_green");
            if (_couponSkinned)
            {
                var title = (RectTransform)_couponPanel.Find("Title");
                GachaUi.Stretch(title, new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.95f));
                title.GetComponent<Text>().color = Palette.NeonGreen;
                GachaUi.Stretch((RectTransform)_couponInput.transform, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.76f));
                OripaLook.Apply((Image)_couponInput.targetGraphic, "input_field");
                OripaLook.Button(cancel, "btn_outline_gray", Palette.Text);
                OripaLook.Button(confirm, "btn_outline_green", Palette.NeonGreen);
                _couponInput.onValueChanged.AddListener(_ => SetCouponError(false));
            }

            _coupon.gameObject.SetActive(false);
        }

        static InputField CreateInput(RectTransform parent, Vector2 min, Vector2 max)
        {
            var box = GachaUi.Panel(parent, "Input", min, max, GachaUi.Inset, Palette.NeonGreen, true);
            var text = GachaUi.Label(box.transform, "Text", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Palette.Text);
            text.supportRichText = false;
            var placeholder = GachaUi.Label(box.transform, "Placeholder", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                GachaUi.Disabled);
            LocText.Bind(placeholder, "ui.coupon.placeholder");
            var input = box.gameObject.AddComponent<InputField>();
            input.targetGraphic = box;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 24;
            return input;
        }

        // 틀린 코드: 빨간 입력칸(input_field_error). 다시 입력하면 원래 칸으로.
        void SetCouponError(bool error)
        {
            if (_couponSkinned)
            {
                OripaLook.Apply((Image)_couponInput.targetGraphic, error ? "input_field_error" : "input_field");
            }
        }

        void OpenCoupon()
        {
            _couponInput.text = string.Empty;
            SetCouponError(false);
            _couponMessage.text = Loc.T("ui.coupon.hint");
            _couponMessage.color = GachaUi.Muted;
            _coupon.SetAsLastSibling();
            _coupon.gameObject.SetActive(true);
            ShowOverlay(_couponGroup, _couponPanel);
            // 모바일은 입력칸을 직접 눌렀을 때 키보드를 띄운다(사용자 터치 없이 포커스를 주면 브라우저가 막거나 화면이 밀린다).
            if (!Application.isMobilePlatform)
            {
                _couponInput.ActivateInputField();
            }
        }

        void CloseCoupon()
        {
            if (!_coupon.gameObject.activeSelf)
            {
                return;
            }

            // 확인·취소·바깥 탭 모두 여기로 온다. 입력칸 포커스를 풀어 모바일 키보드도 같이 닫는다.
            _couponInput.DeactivateInputField();

            HideOverlay(_couponGroup, _couponPanel, () => _coupon.gameObject.SetActive(false));
        }

        void RedeemCoupon()
        {
            // "reset": 모든 저장을 지우고 처음 상태로 다시 시작한다(튜토리얼 확인용).
            if (SaveReset.IsResetCode(_couponInput.text))
            {
                AudioManager.Ensure().PlayUpgradeBuy();
                SaveReset.ResetAll();
                return;
            }

            var dollars = CouponReward(_couponInput.text, Progression.Dollars);
            if (dollars <= 0)
            {
                _couponMessage.text = Loc.T("ui.coupon.invalid");
                _couponMessage.color = Palette.Magenta;
                AudioManager.Ensure().PlayUpgradeDenied();
                if (_couponSkinned)
                {
                    SetCouponError(true);
                    Fx.Shake(_couponInput.transform, 8f, 0.3f);
                }
                else
                {
                    Fx.Shake(_couponPanel, 8f, 0.22f);
                }

                return;
            }

            Progression.AddDollars(dollars);
            AudioManager.Ensure().PlayUpgradeBuy();
            CloseCoupon();
            ShowToast(Loc.F("ui.coupon.applied", GachaUi.Money(dollars)));
        }

        // 쿠폰으로 받을 달러. 틀린 코드면 0. 달러가 int 최대값을 넘지 않게 자른다.
        public static int CouponReward(string code, int currentDollars = 0)
        {
            if (!string.Equals((code ?? string.Empty).Trim(), CouponCode, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return (int)Math.Min(CouponDollars, (long)int.MaxValue - Math.Max(0, currentDollars));
        }

        void ShowToast(string message)
        {
            var panel = (RectTransform)_toast.transform.parent;
            var group = panel.GetComponent<CanvasGroup>();
            _toast.text = message;
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(true);
            group.DOKill(false);
            group.alpha = 1f;
            Fx.Pop(panel);
            group.DOFade(0f, 0.3f).SetDelay(1.2f).SetUpdate(true).SetLink(panel.gameObject)
                .OnComplete(() => panel.gameObject.SetActive(false));
        }

        // 타이틀·결과 화면은 timeScale이 0일 수 있어 모든 연출을 unscaled로 돌린다.
        static void ShowOverlay(CanvasGroup group, RectTransform panel)
        {
            group.DOKill(false);
            panel.DOKill(false);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            panel.localScale = Vector3.one * 0.9f;
            group.DOFade(1f, OpenDuration * 0.8f).SetUpdate(true).SetLink(group.gameObject);
            panel.DOScale(1f, OpenDuration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panel.gameObject);
        }

        static void HideOverlay(CanvasGroup group, RectTransform panel, Action done)
        {
            group.DOKill(false);
            panel.DOKill(false);
            group.blocksRaycasts = false;
            panel.DOScale(0.94f, CloseDuration).SetEase(Ease.InQuad).SetUpdate(true).SetLink(panel.gameObject);
            group.DOFade(0f, CloseDuration).SetUpdate(true).SetLink(group.gameObject).OnComplete(() =>
            {
                panel.localScale = Vector3.one;
                done?.Invoke();
            });
        }

        // 바깥(어두운 배경)을 누르면 닫히게 한다. 패널은 배경의 형제라서 패널을 눌러도 닫히지 않는다.
        static void Clickable(Image image, Action onClick)
        {
            var button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        static Sprite GearSprite()
        {
            if (_gear == null)
            {
                // 임포트가 Multiple로 잘라 둔 경우(icon_settings_0)도 있어서 하위 스프라이트까지 찾는다.
                var sprites = Resources.LoadAll<Sprite>(GearIconResource);
                _gear = sprites != null && sprites.Length > 0 ? sprites[0] : null;
            }

            return _gear;
        }
    }
}
