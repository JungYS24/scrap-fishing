using System;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class TitleView : MonoBehaviour
    {
        // 버전 값은 여기서만 바꾸고, "{0} 버전"/"Ver. {0}" 틀은 번역 표(ui.title.version)를 따른다.
        public const string Version = "1014";
        public static string VersionLabel => Loc.F("ui.title.version", Version);
        public static string MarketLabel => Loc.T("ui.title.oripa_button");
        public static string StartLabel => Loc.T("ui.title.start");
        public const string OripaButtonName = "OripaButton";
        public const string VersionTextName = "VersionText";
        // 출항 버튼과 오리파 버튼 사이 간격(화면 높이 비율).
        public const float MarketGap = 0.02f;
        const float PulseHalf = 0.6f;
        const float PulseScale = 1.05f;
        const float VersionBlinkHalf = 0.7f;
        const float VersionFlashEvery = 2.5f;

        [SerializeField] Button startButton;
        [SerializeField] Button oripaButton;

        Action _onStart;
        Action _onMarket;

        // 결과 화면 버튼이 같은 모양(패널 이미지·테두리·글자)을 따라 쓴다.
        public Button StartButton => startButton;
        public Button MarketButton => oripaButton;
        Action _onSettings;
        RectTransform _marketRect;
        Vector3 _marketScale = Vector3.one;
        UiFxSettings _settings;
        CanvasGroup _group;
        RectTransform _logo;
        RectTransform _buttonRect;
        CanvasGroup _buttonGroup;
        RectTransform _howTo;
        Text _versionText;
        RectTransform _versionRect;
        CanvasGroup _versionGroup;
        Vector3 _logoScale = Vector3.one;
        Vector3 _buttonScale = Vector3.one;
        Vector3 _versionScale = Vector3.one;
        Vector2 _logoHome;
        Sequence _intro;
        Tween _pulseScale;
        Tween _pulseAlpha;
        Tween _versionAlpha;
        Tween _versionColor;
        Sequence _versionFlash;
        Tween _hide;
        bool _allowPulse;
        bool _versionScaleCaptured;

        public void Bind(Action onStart)
        {
            _onStart = onStart;
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStart);
                startButton.onClick.AddListener(HandleStart);
            }
        }

        public void BindMarket(Action onMarket)
        {
            _onMarket = onMarket;
            EnsureMarketButton();
            if (oripaButton != null)
            {
                oripaButton.onClick.RemoveListener(HandleMarket);
                oripaButton.onClick.AddListener(HandleMarket);
            }
        }

        // 예전 음소거 버튼 자리(오른쪽 위)에 설정(톱니) 버튼을 둔다. 음소거는 설정창 안에 있다.
        public void BindSettings(Action onSettings)
        {
            _onSettings = onSettings;
            if (transform.Find("SettingsButton") == null)
            {
                SettingsView.CreateGearButton((RectTransform)transform, new Vector2(0.86f, 0.91f), new Vector2(0.98f, 0.98f), () => _onSettings?.Invoke());
            }
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                gameObject.SetActive(true);
                return;
            }

            HideAnimated();
        }

        void OnEnable()
        {
            Cache();
            PlayIntro();
        }

        void OnDisable()
        {
            _allowPulse = false;
            KillAll();
        }

        void Cache()
        {
            if (_settings == null)
            {
                _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            }

            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null)
                {
                    _group = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_logo == null)
            {
                _logo = transform.Find("TitleImage") as RectTransform;
                if (_logo != null)
                {
                    _logoScale = _logo.localScale;
                    _logoHome = _logo.anchoredPosition;
                }
            }

            if (_buttonRect == null && startButton != null)
            {
                _buttonRect = startButton.transform as RectTransform;
                _buttonScale = _buttonRect.localScale;
                _buttonGroup = startButton.GetComponent<CanvasGroup>();
                if (_buttonGroup == null)
                {
                    _buttonGroup = startButton.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_howTo == null)
            {
                _howTo = transform.Find("HowTo") as RectTransform;
            }

            if (_howTo != null)
            {
                LocText.Bind(_howTo.GetComponent<Text>(), "ui.title.controls");
            }

            EnsureVersion();
            EnsureMarketButton();
            // 씬에 구워 둔 버튼 글자("출항", "오리파!")도 언어를 따라간다.
            LocText.Bind(startButton, "ui.title.start");
            LocText.Bind(oripaButton, "ui.title.oripa_button");
        }

        // 오리파 버튼은 씬 하이러키(Title/OripaButton)에 둔다. 없을 때만 출항 버튼 아래에 같은 크기로 만든다.
        void EnsureMarketButton()
        {
            if (oripaButton == null)
            {
                oripaButton = FindOripaButton(transform);
            }

            if (oripaButton == null && _onMarket != null)
            {
                oripaButton = CreateMarketButton((RectTransform)transform, startButton);
                // UiButton이 붙인 탭 소리 리스너를 지우고, 씬에 있는 버튼과 같이 HandleMarket 하나만 쓴다.
                oripaButton.onClick.RemoveAllListeners();
            }

            if (oripaButton != null && _marketRect == null)
            {
                _marketRect = oripaButton.transform as RectTransform;
                _marketScale = _marketRect.localScale;
            }
        }

        // 로고 바로 위에 버전 글자를 만든다. 로고를 씬에서 옮겼어도 그 위치를 따라간다. 에디터 씬 빌더도 이 함수를 쓴다.
        public static Text CreateVersionText(RectTransform title)
        {
            var text = UiFonts.CreateText(title, VersionTextName, 16, TextAnchor.MiddleCenter);
            text.fontSize = 16;
            text.text = VersionLabel;
            text.color = Palette.NeonGreen;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            var logo = title.Find("TitleImage") as RectTransform;
            if (logo != null)
            {
                rect.anchorMin = new Vector2(logo.anchorMin.x, logo.anchorMax.y);
                rect.anchorMax = new Vector2(logo.anchorMax.x, logo.anchorMax.y);
                rect.offsetMin = new Vector2(logo.offsetMin.x, logo.offsetMax.y + 6f);
                rect.offsetMax = new Vector2(logo.offsetMax.x, logo.offsetMax.y + 30f);
                rect.SetSiblingIndex(logo.GetSiblingIndex() + 1);
            }
            else
            {
                rect.anchorMin = new Vector2(0.2f, 0.8f);
                rect.anchorMax = new Vector2(0.8f, 0.86f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return text;
        }

        // 하이러키의 Title/OripaButton. Button 컴포넌트가 빠져 있으면 붙여서 누를 수 있게 한다.
        public static Button FindOripaButton(Transform title)
        {
            var found = title != null ? title.Find(OripaButtonName) : null;
            if (found == null)
            {
                return null;
            }

            var button = found.GetComponent<Button>();
            if (button == null)
            {
                button = found.gameObject.AddComponent<Button>();
                button.targetGraphic = found.GetComponent<Graphic>();
            }

            return button;
        }

        // 출항 버튼 바로 아래에 같은 크기의 빨간 오리파 버튼을 만든다. 에디터 씬 빌더도 이 함수를 쓴다.
        public static Button CreateMarketButton(RectTransform parent, Button start)
        {
            var min = new Vector2(0.25f, 0.35f);
            var max = new Vector2(0.75f, 0.45f);
            var startRect = start != null ? start.transform as RectTransform : null;
            if (startRect != null)
            {
                var height = startRect.anchorMax.y - startRect.anchorMin.y;
                max = new Vector2(startRect.anchorMax.x, startRect.anchorMin.y - MarketGap);
                min = new Vector2(startRect.anchorMin.x, max.y - height);
            }

            var button = UiButton.Create(parent, OripaButtonName, MarketLabel, min, max, null);
            if (startRect != null)
            {
                button.transform.SetSiblingIndex(startRect.GetSiblingIndex() + 1);
            }

            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = Palette.Magenta;
            }

            var label = button.GetComponentInChildren<Text>(true);
            var startLabel = start != null ? start.GetComponentInChildren<Text>(true) : null;
            if (label != null)
            {
                label.color = Palette.Magenta;
                if (startLabel != null)
                {
                    label.fontSize = startLabel.fontSize;
                }
            }

            return button;
        }

        // 버전 표기는 하이러키(Title/VersionText)에 두고 위치·크기·폰트는 씬에서 관리한다. 글자는 VersionLabel을 쓴다.
        void EnsureVersion()
        {
            var existing = transform.Find(VersionTextName);
            if (existing != null)
            {
                _versionRect = existing as RectTransform;
                _versionText = existing.GetComponent<Text>();
            }

            if (_versionText == null)
            {
                _versionText = CreateVersionText((RectTransform)transform);
                _versionRect = _versionText.rectTransform;
            }

            LocText.Bind(_versionText, () => VersionLabel);
            _versionText.color = Palette.NeonGreen;
            _versionText.raycastTarget = false;
            _versionGroup = _versionText.GetComponent<CanvasGroup>();
            if (_versionGroup == null)
            {
                _versionGroup = _versionText.gameObject.AddComponent<CanvasGroup>();
            }

            _versionGroup.blocksRaycasts = false;
            _versionGroup.interactable = false;
            if (!_versionScaleCaptured && _versionRect != null)
            {
                _versionScale = _versionRect.localScale;
                _versionScaleCaptured = true;
            }
        }

        void PlayIntro()
        {
            Cache();
            _allowPulse = false;
            KillAll();
            _allowPulse = true;
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            StartVersionSparkle();

            var fade = _settings != null ? _settings.titleFadeDuration : 0.28f;
            var logoDur = _settings != null ? _settings.titleLogoDuration : 0.4f;
            var buttonDur = _settings != null ? _settings.titleButtonDuration : 0.22f;
            _intro = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _intro.Append(_group.DOFade(1f, fade));

            if (_logo != null)
            {
                _logo.localScale = _logoScale * 0.82f;
                _logo.anchoredPosition = _logoHome + Vector2.down * 28f;
                _intro.Join(_logo.DOScale(_logoScale, logoDur).SetEase(Ease.OutBack));
                _intro.Join(_logo.DOAnchorPos(_logoHome, logoDur).SetEase(Ease.OutCubic));
            }

            if (_buttonRect != null)
            {
                _buttonRect.localScale = Vector3.zero;
                if (_buttonGroup != null)
                {
                    _buttonGroup.alpha = 1f;
                }

                _intro.Append(_buttonRect.DOScale(_buttonScale, buttonDur).SetEase(Ease.OutBack));
            }

            if (_marketRect != null)
            {
                _marketRect.localScale = Vector3.zero;
                _intro.Append(_marketRect.DOScale(_marketScale, buttonDur).SetEase(Ease.OutBack));
            }

            _intro.OnComplete(BeginPulse);
            _intro.OnKill(BeginPulse);
        }

        void BeginPulse()
        {
            if (!_allowPulse || _buttonRect == null || !isActiveAndEnabled)
            {
                return;
            }

            if (_pulseScale != null && _pulseScale.IsActive())
            {
                return;
            }

            _buttonRect.localScale = _buttonScale;
            if (_buttonGroup == null)
            {
                return;
            }

            _buttonGroup.alpha = 1f;
            _pulseScale = _buttonRect.DOScale(_buttonScale * PulseScale, PulseHalf)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_buttonRect.gameObject);
            _pulseAlpha = _buttonGroup.DOFade(0.45f, PulseHalf)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_buttonRect.gameObject);
        }

        void StartVersionSparkle()
        {
            if (_versionText == null || _versionRect == null || _versionGroup == null)
            {
                return;
            }

            if (_versionAlpha != null && _versionAlpha.IsActive())
            {
                return;
            }

            _versionRect.localScale = _versionScale;
            _versionGroup.alpha = 1f;
            _versionText.color = Palette.NeonGreen;
            _versionAlpha = _versionGroup.DOFade(0.55f, VersionBlinkHalf)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_versionText.gameObject);
            _versionColor = _versionText.DOColor(Palette.Cyan, 1.1f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_versionText.gameObject);
            _versionFlash = DOTween.Sequence().SetUpdate(true).SetLink(_versionText.gameObject);
            _versionFlash.AppendInterval(VersionFlashEvery);
            _versionFlash.Append(_versionRect.DOScale(_versionScale * 1.15f, 0.12f).SetEase(Ease.OutQuad));
            _versionFlash.Append(_versionRect.DOScale(_versionScale, 0.16f).SetEase(Ease.InQuad));
            _versionFlash.SetLoops(-1);
            _versionFlash.OnKill(RestoreVersion);
        }

        void HideAnimated()
        {
            Cache();
            _allowPulse = false;
            StopPulse();
            StopVersionSparkle();
            KillIntro();
            if (_buttonRect != null)
            {
                _buttonRect.localScale = _buttonScale;
            }

            if (!gameObject.activeSelf)
            {
                return;
            }

            _group.blocksRaycasts = false;
            _group.interactable = false;
            var hide = _settings != null ? _settings.titleHideDuration : 0.18f;
            if (_hide != null && _hide.IsActive())
            {
                _hide.Kill(false);
            }

            _hide = _group.DOFade(0f, hide)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => gameObject.SetActive(false));
        }

        void HandleMarket()
        {
            AudioManager.Ensure().PlayUiTap();
            AudioManager.Ensure().Unlock();
            _onMarket?.Invoke();
        }

        void HandleStart()
        {
            _allowPulse = false;
            StopPulse();
            AudioManager.Ensure().Unlock();
            _onStart?.Invoke();
        }

        void StopPulse()
        {
            if (_pulseScale != null && _pulseScale.IsActive())
            {
                _pulseScale.Kill(false);
            }

            if (_pulseAlpha != null && _pulseAlpha.IsActive())
            {
                _pulseAlpha.Kill(false);
            }

            _pulseScale = null;
            _pulseAlpha = null;
            if (_buttonRect != null)
            {
                _buttonRect.localScale = _buttonScale;
            }

            if (_buttonGroup != null)
            {
                _buttonGroup.alpha = 1f;
            }
        }

        void StopVersionSparkle()
        {
            if (_versionAlpha != null && _versionAlpha.IsActive())
            {
                _versionAlpha.Kill(false);
            }

            if (_versionColor != null && _versionColor.IsActive())
            {
                _versionColor.Kill(false);
            }

            if (_versionFlash != null && _versionFlash.IsActive())
            {
                _versionFlash.Kill(false);
            }

            _versionAlpha = null;
            _versionColor = null;
            _versionFlash = null;
            RestoreVersion();
        }

        void RestoreVersion()
        {
            if (_versionRect != null)
            {
                _versionRect.localScale = _versionScale;
            }

            if (_versionGroup != null)
            {
                _versionGroup.alpha = 1f;
            }

            if (_versionText != null)
            {
                _versionText.color = Palette.NeonGreen;
            }
        }

        void KillIntro()
        {
            if (_intro != null && _intro.IsActive())
            {
                _intro.Kill(false);
            }

            _intro = null;
            if (_marketRect != null)
            {
                _marketRect.DOKill(false);
                _marketRect.localScale = _marketScale;
            }
        }

        void KillAll()
        {
            KillIntro();
            StopPulse();
            StopVersionSparkle();
            if (_hide != null && _hide.IsActive())
            {
                _hide.Kill(false);
            }

            transform.DOKill(false);
            if (_logo != null)
            {
                _logo.DOKill(false);
            }

            if (_buttonRect != null)
            {
                _buttonRect.DOKill(false);
            }

            if (_howTo != null)
            {
                _howTo.DOKill(false);
            }
        }
    }
}
