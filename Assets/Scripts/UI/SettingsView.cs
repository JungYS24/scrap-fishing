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
    // 설정창: 음량 3종(전체·배경음·효과음), 기본값, 언어(준비 중), 크레딧, 버전.
    // 타이틀·결과 화면의 톱니 버튼으로 연다. 색·폰트·버튼은 오리파 화면(GachaUi)과 같은 톤을 쓴다.
    public class SettingsView : MonoBehaviour
    {
        // 핸들 기본 크기 40px에 스케일 0.6을 걸어 24px로 보인다. 크기는 여기서 한 번만 정하고 다른 코드가 덮어쓰지 않는다.
        // 터치는 슬라이더 줄 전체(투명 히트 영역)로 받으므로 핸들이 작아도 잡기 쉽다.
        const float HandleSize = 40f;
        public const float HandleScale = 0.6f;
        const float TrackMin = 0.36f;
        const float TrackMax = 0.64f;
        const float MutedRowAlpha = 0.4f;
        const float OpenDuration = 0.2f;
        const float CloseDuration = 0.15f;
        const string LanguageLabel = "언어: 한국어";
        const string GearIconResource = "UI/icon_settings";

        static readonly VolumeChannel[] Channels = { VolumeChannel.Master, VolumeChannel.Bgm, VolumeChannel.Sfx };
        static readonly string[] ChannelNames = { "전체 음량", "배경음", "효과음" };

        // 크레딧 항목. 내용이 빈 항목은 화면에 나오지 않는다(출처를 확인하면 채운다).
        static readonly string[,] Credits =
        {
            { "제작", "정윤성 (2023137028)\n2026-2 네트워크프로그래밍 프로젝트" },
            { "엔진", "Unity 6" },
            { "트윈", "DOTween Pro (Demigiant)" },
            { "폰트", "갈무리 Galmuri11 · Galmuri14\n© Lee Minseo (quiple)\nSIL Open Font License 1.1" },
            { "아트", "Retro Diffusion으로 생성한 도트 에셋" },
            { "잠수 배경", "Underwater Diving 에셋\nLuis Zuno (@ansimuz) · 퍼블릭 도메인" },
            { "효과음 · 타이틀 BGM", "직접 제작" },
            { "플레이 BGM 「Neo-Tokyo Sludge」", "" },
            { "UI 패널 아트", "" }
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
        RectTransform _credits;
        CanvasGroup _creditsGroup;
        RectTransform _creditsPanel;
        ScrollRect _creditsScroll;
        Text _toast;
        readonly Slider[] _sliders = new Slider[3];
        readonly Text[] _values = new Text[3];
        readonly CanvasGroup[] _rows = new CanvasGroup[3];
        Text _volumeHeader;
        bool _syncing;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Build(RectTransform area)
        {
            _root = GachaUi.Rect(area, "Settings", Vector2.zero, Vector2.one);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            Clickable(GachaUi.Panel(_root, "Dim", Vector2.zero, Vector2.one, GachaUi.Dim, null, true), Close);

            _panel = GachaUi.Panel(_root, "Panel", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f), GachaUi.Card, Palette.NeonGreen, true).rectTransform;
            GachaUi.Label(_panel, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.905f), new Vector2(0.8f, 0.98f), Palette.Text, "설정");
            GachaUi.MakeButton(_panel, "Close", "X", new Vector2(0.84f, 0.91f), new Vector2(0.96f, 0.975f), Palette.Magenta, 28, Close);

            _volumeHeader = GachaUi.Label(_panel, "VolumeHeader", 16, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.845f), new Vector2(0.8f, 0.895f), GachaUi.Muted, "음량");
            SoundToggle.Create(_panel, new Vector2(0.84f, 0.848f), new Vector2(0.94f, 0.895f));
            for (var i = 0; i < Channels.Length; i++)
            {
                var top = 0.835f - i * 0.135f;
                BuildRow(i, new Vector2(0.06f, top - 0.125f), new Vector2(0.94f, top));
            }

            GachaUi.MakeButton(_panel, "Defaults", "기본값으로", new Vector2(0.06f, 0.355f), new Vector2(0.94f, 0.425f), Palette.Cyan, 22, ResetDefaults);
            GachaUi.MakeButton(_panel, "Language", LanguageLabel, new Vector2(0.06f, 0.265f), new Vector2(0.94f, 0.335f), GachaUi.Muted, 22,
                () => ShowToast("준비 중입니다"));
            GachaUi.MakeButton(_panel, "Credits", "크레딧", new Vector2(0.06f, 0.175f), new Vector2(0.94f, 0.245f), Palette.Gold, 22, OpenCredits);
            GachaUi.MakeButton(_panel, "Coupon", "쿠폰", new Vector2(0.06f, 0.09f), new Vector2(0.94f, 0.16f), Palette.NeonGreen, 22, OpenCoupon);
            GachaUi.Label(_panel, "Version", 16, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.075f), GachaUi.Muted,
                TitleView.VersionLabel);

            var toast = GachaUi.Panel(_panel, "Toast", new Vector2(0.12f, 0.085f), new Vector2(0.88f, 0.16f), GachaUi.Inset, Palette.NeonGreen);
            _toast = GachaUi.Label(toast.transform, "Text", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Palette.NeonGreen);
            toast.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            toast.gameObject.SetActive(false);

            BuildCredits();
            BuildCoupon();
            VolumeSettings.Changed += Sync;
            AudioManager.SoundChanged += Sync;
            _root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            VolumeSettings.Changed -= Sync;
            AudioManager.SoundChanged -= Sync;
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

        // 이름과 숫자를 윗줄에, 손가락으로 끌기 쉬운 넓은 슬라이더를 아랫줄에 둔다.
        void BuildRow(int index, Vector2 min, Vector2 max)
        {
            var channel = Channels[index];
            var row = GachaUi.Rect(_panel, "Row_" + channel, min, max);
            _rows[index] = row.gameObject.AddComponent<CanvasGroup>();
            GachaUi.Label(row, "Name", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.56f), new Vector2(0.7f, 1f), Palette.Text, ChannelNames[index]);
            _values[index] = GachaUi.Label(row, "Value", 22, TextAnchor.MiddleRight, new Vector2(0.7f, 0.56f), new Vector2(1f, 1f), Palette.NeonGreen);

            var slider = CreateSlider(row, new Vector2(0f, 0f), new Vector2(1f, 0.56f));
            slider.onValueChanged.AddListener(value =>
            {
                if (!_syncing)
                {
                    VolumeSettings.Set(channel, Mathf.RoundToInt(value), false);
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

        static Slider CreateSlider(RectTransform parent, Vector2 min, Vector2 max)
        {
            var root = GachaUi.Rect(parent, "Slider", min, max);
            // 줄 전체를 잡을 수 있게 투명한 히트 영역을 깐다.
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var pad = HandleSize * HandleScale * 0.5f;
            var track = GachaUi.Panel(root, "Track", new Vector2(0f, TrackMin), new Vector2(1f, TrackMax), GachaUi.Inset, GachaUi.Muted);
            track.rectTransform.offsetMin = new Vector2(pad, 0f);
            track.rectTransform.offsetMax = new Vector2(-pad, 0f);

            var fillArea = GachaUi.Rect(root, "FillArea", new Vector2(0f, TrackMin), new Vector2(1f, TrackMax));
            fillArea.offsetMin = new Vector2(pad, 0f);
            fillArea.offsetMax = new Vector2(-pad, 0f);
            var fill = GachaUi.Panel(fillArea, "Fill", Vector2.zero, Vector2.one, Palette.NeonGreen);

            var handleArea = GachaUi.Rect(root, "HandleArea", Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(pad, 0f);
            handleArea.offsetMax = new Vector2(-pad, 0f);
            var handle = GachaUi.Panel(handleArea, "Handle", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Palette.Text, Palette.NeonGreen, true);
            handle.rectTransform.sizeDelta = new Vector2(HandleSize, HandleSize);
            handle.rectTransform.localScale = new Vector3(HandleScale, HandleScale, 1f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = VolumeSettings.MaxValue;
            slider.wholeNumbers = true;
            return slider;
        }

        // 음소거는 슬라이더 값과 별개로 동작한다. 음소거 중이면 값은 그대로 두고 줄을 흐리게, 머리글에 표시한다.
        void Sync()
        {
            var muted = !AudioManager.Ensure().SoundOn;
            if (_volumeHeader != null)
            {
                _volumeHeader.text = muted ? "음량 · 음소거 중" : "음량";
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
                }

                if (_values[i] != null)
                {
                    _values[i].text = value.ToString();
                }
            }

            _syncing = false;
        }

        void ResetDefaults()
        {
            VolumeSettings.ResetDefaults();
            Sync();
            ShowToast("기본값으로 되돌렸어요");
        }

        void BuildCredits()
        {
            _credits = GachaUi.Rect(_root, "CreditsOverlay", Vector2.zero, Vector2.one);
            _creditsGroup = _credits.gameObject.AddComponent<CanvasGroup>();
            Clickable(GachaUi.Panel(_credits, "Dim", Vector2.zero, Vector2.one, GachaUi.Dim, null, true), CloseCredits);

            _creditsPanel = GachaUi.Panel(_credits, "Panel", new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f), GachaUi.Card, Palette.Gold, true).rectTransform;
            GachaUi.Label(_creditsPanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), Palette.Gold, "크레딧");
            var list = GachaUi.Scroll(_creditsPanel, "List", new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.89f), 12f, out _creditsScroll);
            for (var i = 0; i < Credits.GetLength(0); i++)
            {
                var head = Credits[i, 0];
                var body = Credits[i, 1];
                if (string.IsNullOrEmpty(body))
                {
                    continue;
                }

                var entry = GachaUi.Label(list, "Credit" + i, 22, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, Palette.Text,
                    "<color=" + GachaUi.Hex(Palette.NeonGreen) + "><size=16>" + head + "</size></color>\n" + body);
                entry.horizontalOverflow = HorizontalWrapMode.Wrap;
                entry.verticalOverflow = VerticalWrapMode.Overflow;
                entry.lineSpacing = 1.1f;
            }

            GachaUi.MakeButton(_creditsPanel, "Close", "닫기", new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.11f), GachaUi.Muted, 22, CloseCredits);
            _credits.gameObject.SetActive(false);
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

            _couponPanel = GachaUi.Panel(_coupon, "Panel", new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.64f), GachaUi.Card, Palette.NeonGreen, true).rectTransform;
            GachaUi.Label(_couponPanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f), Palette.NeonGreen, "쿠폰 입력");
            _couponInput = CreateInput(_couponPanel, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.74f));
            _couponMessage = GachaUi.Label(_couponPanel, "Message", 16, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.48f), GachaUi.Muted,
                "쿠폰 코드를 입력하세요");
            GachaUi.MakeButton(_couponPanel, "Cancel", "취소", new Vector2(0.08f, 0.07f), new Vector2(0.46f, 0.3f), GachaUi.Muted, 22, CloseCoupon);
            GachaUi.MakeButton(_couponPanel, "Confirm", "확인", new Vector2(0.54f, 0.07f), new Vector2(0.92f, 0.3f), Palette.NeonGreen, 22, RedeemCoupon);
            _coupon.gameObject.SetActive(false);
        }

        static InputField CreateInput(RectTransform parent, Vector2 min, Vector2 max)
        {
            var box = GachaUi.Panel(parent, "Input", min, max, GachaUi.Inset, Palette.NeonGreen, true);
            var text = GachaUi.Label(box.transform, "Text", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Palette.Text);
            text.supportRichText = false;
            var placeholder = GachaUi.Label(box.transform, "Placeholder", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                GachaUi.Disabled, "코드");
            var input = box.gameObject.AddComponent<InputField>();
            input.targetGraphic = box;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 24;
            return input;
        }

        void OpenCoupon()
        {
            _couponInput.text = string.Empty;
            _couponMessage.text = "쿠폰 코드를 입력하세요";
            _couponMessage.color = GachaUi.Muted;
            _coupon.SetAsLastSibling();
            _coupon.gameObject.SetActive(true);
            ShowOverlay(_couponGroup, _couponPanel);
            _couponInput.ActivateInputField();
        }

        void CloseCoupon()
        {
            if (!_coupon.gameObject.activeSelf)
            {
                return;
            }

            HideOverlay(_couponGroup, _couponPanel, () => _coupon.gameObject.SetActive(false));
        }

        void RedeemCoupon()
        {
            var dollars = CouponReward(_couponInput.text, Progression.Dollars);
            if (dollars <= 0)
            {
                _couponMessage.text = "올바르지 않은 쿠폰이에요";
                _couponMessage.color = Palette.Magenta;
                AudioManager.Ensure().PlayUpgradeDenied();
                Fx.Shake(_couponPanel, 8f, 0.22f);
                return;
            }

            Progression.AddDollars(dollars);
            AudioManager.Ensure().PlayUpgradeBuy();
            CloseCoupon();
            ShowToast("쿠폰 적용 +" + GachaUi.Money(dollars));
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
