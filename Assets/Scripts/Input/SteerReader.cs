using System;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Controls
{
    [DefaultExecutionOrder(-100)]
    public class SteerReader : MonoBehaviour
    {
        [SerializeField] float deadzonePixels = 6f;

        public static bool BlocksTapAndSwipe { get; private set; }

        public event Action<float> OnWorldDeltaX;
        public event Action<SwipeInfo> OnSegment;

        Camera _camera;
        Func<bool> _accepts;
        Vector2 _pressPosition;
        Vector2 _lastPosition;
        bool _tracking;
        bool _armed;
        bool _latched;

        public void Bind(Camera camera, Func<bool> acceptsSteer)
        {
            _camera = camera;
            _accepts = acceptsSteer;
        }

        void Update()
        {
            if (!PointerUtil.TryRead(out var position, out var pressedThisFrame, out var releasedThisFrame))
            {
                _tracking = false;
                PublishBlock();
                return;
            }

            if (pressedThisFrame)
            {
                _tracking = false;
                _armed = false;
                _latched = false;
                if (FixedResolution.ContainsWindowPoint(position) && !PointerUtil.IsOverUi(position))
                {
                    _tracking = true;
                    _pressPosition = position;
                    _lastPosition = position;
                }
            }

            if (_tracking)
            {
                var cam = _camera != null ? _camera : Camera.main;
                if (!_armed && GameDistance(_pressPosition, position) >= deadzonePixels)
                {
                    _armed = true;
                }

                var accepts = _armed && cam != null && _accepts != null && _accepts();
                if (accepts && position != _lastPosition)
                {
                    var previous = FixedResolution.WindowToWorld(cam, _lastPosition);
                    var current = FixedResolution.WindowToWorld(cam, position);
                    previous.z = 0f;
                    current.z = 0f;
                    var deltaX = current.x - previous.x;
                    if (Mathf.Abs(deltaX) > 0.00001f)
                    {
                        OnWorldDeltaX?.Invoke(deltaX);
                    }

                    _latched = true;
                    OnSegment?.Invoke(new SwipeInfo(_lastPosition, position, previous, current));
                }

                _lastPosition = position;
                if (releasedThisFrame)
                {
                    _tracking = false;
                }
            }

            PublishBlock();
        }

        void LateUpdate()
        {
            if (_tracking)
            {
                return;
            }

            _armed = false;
            _latched = false;
            BlocksTapAndSwipe = false;
        }

        void PublishBlock()
        {
            BlocksTapAndSwipe = _latched;
        }

        static float GameDistance(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(FixedResolution.ToGamePixels(a), FixedResolution.ToGamePixels(b));
        }
    }
}
