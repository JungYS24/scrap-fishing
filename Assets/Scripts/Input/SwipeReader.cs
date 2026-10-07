using System;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Controls
{
    public readonly struct SwipeInfo
    {
        public readonly Vector2 StartScreen;
        public readonly Vector2 EndScreen;
        public readonly Vector3 StartWorld;
        public readonly Vector3 EndWorld;

        public bool IsDownward => EndWorld.y < StartWorld.y - 0.25f;

        public SwipeInfo(Vector2 startScreen, Vector2 endScreen, Vector3 startWorld, Vector3 endWorld)
        {
            StartScreen = startScreen;
            EndScreen = endScreen;
            StartWorld = startWorld;
            EndWorld = endWorld;
        }
    }

    public class SwipeReader : MonoBehaviour
    {
        [SerializeField] float minPixels = 40f;

        public event Action OnBegin;
        public event Action<SwipeInfo> OnDrag;
        public event Action<SwipeInfo> OnSwipe;

        Camera _camera;
        Vector2 _pressPosition;
        Vector2 _lastPosition;
        bool _pressed;
        bool _dragging;

        public void Bind(Camera camera)
        {
            _camera = camera;
        }

        void Update()
        {
            if (!PointerUtil.TryRead(out var position, out var pressedThisFrame, out var releasedThisFrame))
            {
                return;
            }

            if (pressedThisFrame)
            {
                _pressed = false;
                if (!FixedResolution.ContainsWindowPoint(position) || PointerUtil.IsOverUi(position))
                {
                    return;
                }

                _pressed = true;
                _dragging = false;
                _pressPosition = position;
                _lastPosition = position;
                OnBegin?.Invoke();
            }

            if (!_pressed)
            {
                return;
            }

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                return;
            }

            if (!_dragging && GameDistance(_pressPosition, position) >= minPixels)
            {
                _dragging = true;
                _lastPosition = _pressPosition;
            }

            if (_dragging && position != _lastPosition)
            {
                OnDrag?.Invoke(Build(cam, _lastPosition, position));
            }

            _lastPosition = position;
            if (!releasedThisFrame)
            {
                return;
            }

            _pressed = false;
            if (_dragging)
            {
                OnSwipe?.Invoke(Build(cam, _pressPosition, position));
            }
        }

        static float GameDistance(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(FixedResolution.ToGamePixels(a), FixedResolution.ToGamePixels(b));
        }

        static SwipeInfo Build(Camera cam, Vector2 start, Vector2 end)
        {
            var startWorld = FixedResolution.WindowToWorld(cam, start);
            var endWorld = FixedResolution.WindowToWorld(cam, end);
            startWorld.z = 0f;
            endWorld.z = 0f;
            return new SwipeInfo(start, end, startWorld, endWorld);
        }
    }
}
