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
        RectTransform _knob;
        Vector2 _originGame;

        public void Build(Transform canvas)
        {
            _root = new GameObject("Joystick");
            _root.transform.SetParent(canvas, false);
            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(100f, 120f);
            rootRect.sizeDelta = new Vector2(160f, 160f);

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

            _originGame = new Vector2(100f, 120f);
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
            if (_root == null || !_root.activeInHierarchy)
            {
                return;
            }

            if (!PointerUtil.TryRead(out var position, out var pressedThisFrame, out var releasedThisFrame))
            {
                return;
            }

            var game = FixedResolution.ToGamePixels(position);
            if (pressedThisFrame && FixedResolution.ContainsWindowPoint(position) && Vector2.Distance(game, _originGame) <= PadRadius * 1.35f)
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

            var delta = Vector2.ClampMagnitude(game - _originGame, PadRadius);
            Value = delta / PadRadius;
            if (_knob != null)
            {
                _knob.anchoredPosition = delta;
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
