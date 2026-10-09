using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Controls
{
    public class VirtualJoystick : MonoBehaviour
    {
        const float PadRadius = 80f;

        public Vector2 Value { get; private set; }
        public bool IsHeld { get; private set; }

        GameObject _root;
        RectTransform _rootRect;
        RectTransform _knob;
        Canvas _canvas;

        public void Build(Transform canvas)
        {
            _root = new GameObject("Joystick");
            _root.transform.SetParent(canvas, false);
            _rootRect = _root.AddComponent<RectTransform>();
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.zero;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.anchoredPosition = new Vector2(100f, 120f);
            _rootRect.sizeDelta = new Vector2(160f, 160f);
            _canvas = _root.GetComponentInParent<Canvas>();

            var baseImage = _root.AddComponent<Image>();
            baseImage.sprite = PlaceholderFactory.Circle(new Color(0.06f, 0.08f, 0.12f, 0.72f), 64);
            baseImage.raycastTarget = false;

            var knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(_root.transform, false);
            _knob = knobGo.AddComponent<RectTransform>();
            _knob.anchorMin = new Vector2(0.5f, 0.5f);
            _knob.anchorMax = new Vector2(0.5f, 0.5f);
            _knob.pivot = new Vector2(0.5f, 0.5f);
            _knob.sizeDelta = new Vector2(64f, 64f);
            _knob.anchoredPosition = Vector2.zero;
            var knobImage = knobGo.AddComponent<Image>();
            knobImage.sprite = PlaceholderFactory.Circle(Palette.NeonGreen, 32);
            knobImage.raycastTarget = false;

            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }

            if (!visible)
            {
                Release();
            }
        }

        void Update()
        {
            if (_root == null || !_root.activeInHierarchy || _rootRect == null)
            {
                return;
            }

            if (!PointerUtil.TryRead(out var position, out var pressedThisFrame, out var releasedThisFrame))
            {
                return;
            }

            var origin = (Vector2)RectTransformUtility.WorldToScreenPoint(null, _rootRect.position);
            var scale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            var delta = (position - origin) / scale;
            if (pressedThisFrame && FixedResolution.ContainsWindowPoint(position) && delta.magnitude <= PadRadius * 1.35f)
            {
                IsHeld = true;
            }

            if (!IsHeld)
            {
                return;
            }

            if (releasedThisFrame)
            {
                Release();
                return;
            }

            var clamped = Vector2.ClampMagnitude(delta, PadRadius);
            Value = clamped / PadRadius;
            if (_knob != null)
            {
                _knob.anchoredPosition = clamped;
            }
        }

        public void Release()
        {
            IsHeld = false;
            Value = Vector2.zero;
            if (_knob != null)
            {
                _knob.anchoredPosition = Vector2.zero;
            }
        }
    }
}
