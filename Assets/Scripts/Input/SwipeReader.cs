using System;
using UnityEngine;

namespace ScrapFishing.Controls
{
    public readonly struct SwipeInfo
    {
        public readonly Vector2 StartScreen;
        public readonly Vector2 EndScreen;
        public readonly Vector3 StartWorld;
        public readonly Vector3 EndWorld;

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
        [SerializeField] float minPixels = 70f;

        public event Action<SwipeInfo> OnSwipe;

        Camera _camera;
        Vector2 _pressPosition;
        bool _pressed;

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
                _pressed = true;
                _pressPosition = position;
            }

            if (!_pressed || !releasedThisFrame)
            {
                return;
            }

            _pressed = false;
            if (Vector2.Distance(_pressPosition, position) < minPixels)
            {
                return;
            }

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                return;
            }

            var startWorld = cam.ScreenToWorldPoint(new Vector3(_pressPosition.x, _pressPosition.y, 10f));
            var endWorld = cam.ScreenToWorldPoint(new Vector3(position.x, position.y, 10f));
            startWorld.z = 0f;
            endWorld.z = 0f;
            OnSwipe?.Invoke(new SwipeInfo(_pressPosition, position, startWorld, endWorld));
        }
    }
}
