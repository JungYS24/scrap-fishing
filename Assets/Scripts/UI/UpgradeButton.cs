using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class UpgradeButton : MonoBehaviour
    {
        [SerializeField] UpgradeKind kind;
        [SerializeField] Button button;
        [SerializeField] Text nameText;
        [SerializeField] Text levelText;
        [SerializeField] Text costText;
        [SerializeField] GameObject costIcon;

        UiFxSettings _settings;
        RectTransform _rect;
        Vector3 _baseScale = Vector3.one;
        Tween _feedback;

        void Awake()
        {
            _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            _rect = transform as RectTransform;
            _baseScale = _rect != null ? _rect.localScale : Vector3.one;
            if (button != null)
            {
                button.onClick.AddListener(Buy);
            }
        }

        void OnDisable()
        {
            KillFeedback();
            if (_rect != null)
            {
                _rect.localScale = _baseScale;
            }
        }

        void Buy()
        {
            if (Progression.TryBuy(kind))
            {
                AudioManager.Ensure().PlayCatch();
                Punch();
            }
            else
            {
                Shake();
            }
        }

        public void Refresh()
        {
            var level = Progression.Level(kind);
            var maxed = Progression.IsMaxed(kind);
            var cost = Progression.Cost(kind);
            if (nameText != null)
            {
                nameText.text = Progression.DisplayName(kind);
            }

            if (levelText != null)
            {
                levelText.text = $"Lv {level}/{Progression.MaxLevel(kind)}";
            }

            if (costText != null)
            {
                costText.text = maxed ? "MAX" : cost.ToString();
            }

            if (costIcon != null)
            {
                costIcon.SetActive(!maxed);
            }

            if (button != null)
            {
                button.interactable = !maxed;
            }
        }

        public void PlayPop(float delay)
        {
            if (_rect == null)
            {
                return;
            }

            KillFeedback();
            var duration = _settings != null ? _settings.readyPopDuration : 0.22f;
            _rect.localScale = Vector3.zero;
            _feedback = _rect.DOScale(_baseScale, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        void Punch()
        {
            if (_rect == null || _settings == null)
            {
                return;
            }

            KillFeedback();
            _rect.localScale = _baseScale;
            _feedback = _rect.DOPunchScale(Vector3.one * _settings.punchScale, _settings.punchDuration, 4, 0.4f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        void Shake()
        {
            if (_rect == null || _settings == null)
            {
                return;
            }

            KillFeedback();
            _rect.localScale = _baseScale;
            _feedback = _rect.DOShakePosition(_settings.shakeDuration, _settings.shakeStrength, 12, 90f, false, true)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        void KillFeedback()
        {
            if (_feedback != null && _feedback.IsActive())
            {
                _feedback.Kill(false);
            }

            _feedback = null;
            if (_rect != null)
            {
                _rect.DOKill(false);
            }
        }
    }
}
