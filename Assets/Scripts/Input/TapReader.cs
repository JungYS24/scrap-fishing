using System;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Controls
{
    public class TapReader : MonoBehaviour
    {
        [SerializeField] float slopPixels = 40f;

        public event Action OnTap;

        Vector2 _pressPosition;
        bool _pressed;

        void Update()
        {
            if (!PointerUtil.TryRead(out var position, out var pressedThisFrame, out var releasedThisFrame))
            {
                return;
            }

            if (pressedThisFrame)
            {
                if (!FixedResolution.ContainsWindowPoint(position))
                {
                    return;
                }

                _pressed = true;
                _pressPosition = position;
            }

            if (!_pressed || !releasedThisFrame)
            {
                return;
            }

            _pressed = false;
            if (Vector2.Distance(FixedResolution.ToGamePixels(_pressPosition), FixedResolution.ToGamePixels(position)) <= slopPixels)
            {
                OnTap?.Invoke();
            }
        }
    }
}
