using System;
using DG.Tweening;
using ScrapFishing.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class TitleView : MonoBehaviour
    {
        const float PulseHalf = 0.6f;
        const float PulseScale = 1.05f;

        [SerializeField] Button startButton;

        Action _onStart;
        UiFxSettings _settings;
        CanvasGroup _group;
        RectTransform _logo;
        RectTransform _buttonRect;
        CanvasGroup _buttonGroup;
        RectTransform _howTo;
        Vector3 _logoScale = Vector3.one;
        Vector3 _buttonScale = Vector3.one;
        Vector2 _logoHome;
        Sequence _intro;
        Tween _pulseScale;
        Tween _pulseAlpha;
        Tween _hide;
        bool _allowPulse;

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

        void HideAnimated()
        {
            Cache();
            _allowPulse = false;
            StopPulse();
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
