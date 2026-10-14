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
        public const string VersionLabel = "1014 버전";
        const float PulseHalf = 0.6f;
        const float PulseScale = 1.05f;
        const float VersionBlinkHalf = 0.7f;
        const float VersionFlashEvery = 2.5f;

        [SerializeField] Button startButton;

        Action _onStart;
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
                var howTo = _howTo.GetComponent<Text>();
                if (howTo != null)
                {
                    howTo.text = "깊게 탭할수록 고가\n좌우로 스와이프해서 루어 조종\n아래로 스와이프 잠수\n스틱 유영";
                }
            }

            EnsureVersion();
        }

        void EnsureVersion()
        {
            var existing = transform.Find("VersionText");
            if (existing != null)
            {
                _versionRect = existing as RectTransform;
                _versionText = existing.GetComponent<Text>();
            }

            if (_versionText == null)
            {
                _versionText = UiFonts.CreateText(transform, "VersionText", 16, TextAnchor.MiddleCenter);
                _versionText.fontSize = 16;
                _versionRect = _versionText.rectTransform;
                if (_logo != null)
                {
                    _versionRect.anchorMin = new Vector2(_logo.anchorMin.x, _logo.anchorMax.y);
                    _versionRect.anchorMax = new Vector2(_logo.anchorMax.x, _logo.anchorMax.y);
                    _versionRect.pivot = new Vector2(0.5f, 0f);
                    _versionRect.sizeDelta = new Vector2(0f, 24f);
                    _versionRect.anchoredPosition = new Vector2(0f, 6f);
                    _versionRect.SetSiblingIndex(_logo.GetSiblingIndex() + 1);
                }
                else
                {
                    _versionRect.anchorMin = new Vector2(0.2f, 0.8f);
                    _versionRect.anchorMax = new Vector2(0.8f, 0.86f);
                    _versionRect.offsetMin = Vector2.zero;
                    _versionRect.offsetMax = Vector2.zero;
                }
            }

            _versionText.text = VersionLabel;
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
