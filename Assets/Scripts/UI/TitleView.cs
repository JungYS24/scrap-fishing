using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class TitleView : MonoBehaviour
    {
        [SerializeField] Button startButton;

        Action _onStart;
        UiFxSettings _settings;
        CanvasGroup _group;
        RectTransform _logo;
        RectTransform _buttonRect;
        RectTransform _howTo;
        Vector3 _logoScale = Vector3.one;
        Vector3 _buttonScale = Vector3.one;
        Vector2 _logoHome;
        Sequence _intro;
        Tween _pulse;
        Tween _hide;

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
            }

            if (_howTo == null)
            {
                _howTo = transform.Find("HowTo") as RectTransform;
            }
        }

        void PlayIntro()
        {
            Cache();
            KillAll();
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
                _intro.Append(_buttonRect.DOScale(_buttonScale, buttonDur).SetEase(Ease.OutBack));
                _intro.OnComplete(StartPulse);
            }
        }

        void StartPulse()
        {
            if (_buttonRect == null || _settings != null && _settings.Reduced)
            {
                return;
            }

            var pulse = _settings != null ? _settings.titlePulseScale : 0.06f;
            var duration = _settings != null ? _settings.titlePulseDuration : 0.75f;
            _buttonRect.localScale = _buttonScale;
            _pulse = _buttonRect.DOScale(_buttonScale * (1f + pulse), duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_buttonRect.gameObject);
        }

        void HideAnimated()
        {
            Cache();
            KillIntro();
            if (_pulse != null && _pulse.IsActive())
            {
                _pulse.Kill(false);
            }

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
            _onStart?.Invoke();
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
            if (_pulse != null && _pulse.IsActive())
            {
                _pulse.Kill(false);
            }

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
