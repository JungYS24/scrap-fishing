using System;
using DG.Tweening;
using ScrapFishing.FX;
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
        Tween _colorTween;
        Vector3 _baseScale = Vector3.one;
        bool _scaleCaptured;
        float _displayed;
        int _written = int.MinValue;
        Color _flashColor;

        public int Displayed => Mathf.RoundToInt(_displayed);
        public event Action<int> Stepped;
        public event Action Completed;

        public void Bind(UiFxSettings fxSettings, string numberPrefix, bool comma, string numberSuffix = "", bool useUnscaled = true)
        {
            settings = fxSettings;
            prefix = numberPrefix;
            suffix = numberSuffix;
            thousandsComma = comma;
            unscaled = useUnscaled;
            EnsureText();
            baseColor = _text.color;
            CaptureScale();
        }

        // 언어가 바뀌면 앞 글자만 바꾸고 지금 보이는 숫자를 다시 쓴다(세는 중이면 다음 단계에서 반영).
        public void SetPrefix(string numberPrefix)
        {
            prefix = numberPrefix;
            EnsureText();
            if (_countTween == null || !_countTween.IsActive())
            {
                _written = int.MinValue;
                Write(Displayed);
            }
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
                .SetLink(gameObject)
                .OnComplete(() => Completed?.Invoke());
            PlayFeedback(target >= from);
        }

        void Awake()
        {
            EnsureText();
            CaptureScale();
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
            }
        }

        void CaptureScale()
        {
            if (_scaleCaptured)
            {
                return;
            }

            _baseScale = transform.localScale;
            _scaleCaptured = true;
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
            if (_countTween != null && _countTween.IsActive())
            {
                Stepped?.Invoke(value);
            }

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
            var rect = (RectTransform)_text.transform;
            if (_colorTween != null && _colorTween.IsActive())
            {
                _colorTween.Kill(false);
            }

            rect.localScale = _baseScale;
            _text.color = baseColor;
            if (gained)
            {
                Fx.Punch(rect, settings.punchScale, settings.punchDuration);
                _flashColor = settings.gainFlash;
                _colorTween = _text.DOColor(_flashColor, settings.flashDuration)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetUpdate(unscaled)
                    .SetLink(gameObject)
                    .OnKill(() => _text.color = baseColor);
            }
            else
            {
                Fx.Shake(rect, settings.shakeStrength, settings.shakeDuration);
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
