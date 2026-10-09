using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    [RequireComponent(typeof(Text))]
    public class CountingText : MonoBehaviour
    {
        [SerializeField] UiFxSettings settings;
        [SerializeField] string prefix = string.Empty;
        [SerializeField] string suffix = string.Empty;
        [SerializeField] bool thousandsComma;
        [SerializeField] Color baseColor = Color.white;
        [SerializeField] bool unscaled = true;

        Text _text;
        Tweener _countTween;
        Tween _punchTween;
        Tween _colorTween;
        Tween _shakeTween;
        Vector3 _baseScale = Vector3.one;
        float _displayed;
        int _written = int.MinValue;
        Color _flashColor;

        public int Displayed => Mathf.RoundToInt(_displayed);

        public void Bind(UiFxSettings fxSettings, string numberPrefix, bool comma, string numberSuffix = "", bool useUnscaled = true)
        {
            settings = fxSettings;
            prefix = numberPrefix;
            suffix = numberSuffix;
            thousandsComma = comma;
            unscaled = useUnscaled;
            EnsureText();
            baseColor = _text.color;
            _baseScale = transform.localScale;
        }

        public void Snap(int value)
        {
            KillCount(false);
            _displayed = value;
            Write(value);
        }

        public void PlayTo(int target)
        {
            EnsureText();
            if (settings == null)
            {
                Snap(target);
                return;
            }

            var from = _displayed;
            if (Mathf.RoundToInt(from) == target && _countTween != null && _countTween.IsActive())
            {
                return;
            }

            KillCount(false);
            var duration = Mathf.Max(0.01f, settings.countDuration);
            _countTween = DOTween.To(() => _displayed, Apply, target, duration)
                .SetEase(settings.countEase)
                .SetUpdate(unscaled)
                .SetLink(gameObject);
            PlayFeedback(target >= from);
        }

        void Awake()
        {
            EnsureText();
        }

        void OnDisable()
        {
            KillAll();
        }

        void EnsureText()
        {
            if (_text == null)
            {
                _text = GetComponent<Text>();
                if (_text != null)
                {
                    _baseScale = transform.localScale;
                }
            }
        }

        void Apply(float value)
        {
            _displayed = value;
            Write(Mathf.RoundToInt(value));
        }

        void Write(int value)
        {
            if (value == _written && _text != null && !string.IsNullOrEmpty(_text.text))
            {
                return;
            }

            _written = value;
            if (_text == null)
            {
                return;
            }

            if (thousandsComma)
            {
                _text.text = prefix + value.ToString("N0") + suffix;
            }
            else
            {
                _text.text = prefix + value + suffix;
            }
        }

        void PlayFeedback(bool gained)
        {
            if (settings != null && settings.Reduced)
            {
                return;
            }

            var rect = (RectTransform)_text.transform;
            if (_punchTween != null && _punchTween.IsActive())
            {
                _punchTween.Kill(false);
            }

            if (_colorTween != null && _colorTween.IsActive())
            {
                _colorTween.Kill(false);
            }

            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Kill(false);
            }

            rect.localScale = _baseScale;
            _text.color = baseColor;
            if (gained)
            {
                _punchTween = rect.DOPunchScale(Vector3.one * settings.punchScale, settings.punchDuration, 4, 0.4f)
                    .SetUpdate(unscaled)
                    .SetLink(gameObject);
                _flashColor = settings.gainFlash;
                _colorTween = _text.DOColor(_flashColor, settings.flashDuration)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetUpdate(unscaled)
                    .SetLink(gameObject)
                    .OnKill(() => _text.color = baseColor);
            }
            else
            {
                _shakeTween = rect.DOShakePosition(settings.shakeDuration, settings.shakeStrength, 12, 90f, false, true)
                    .SetUpdate(unscaled)
                    .SetLink(gameObject);
                _flashColor = settings.loseFlash;
                _colorTween = _text.DOColor(_flashColor, settings.flashDuration)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetUpdate(unscaled)
                    .SetLink(gameObject)
                    .OnKill(() => _text.color = baseColor);
            }
        }

        void KillCount(bool complete)
        {
            if (_countTween != null && _countTween.IsActive())
            {
                _countTween.Kill(complete);
            }

            _countTween = null;
        }

        void KillAll()
        {
            KillCount(false);
            transform.DOKill(false);
            if (_text != null)
            {
                _text.DOKill(false);
                _text.color = baseColor;
                transform.localScale = _baseScale;
            }
        }
    }
}
