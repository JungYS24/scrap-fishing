using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ScrapFishing.Controls
{
    public static class PointerUtil
    {
        const float EmulatedMouseWindow = 0.6f;
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
        static float _lastTouchTime = -1f;

        public static bool TryRead(out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame)
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var press = touch.primaryTouch.press;
                if (press.isPressed || press.wasPressedThisFrame || press.wasReleasedThisFrame)
                {
                    _lastTouchTime = Time.unscaledTime;
                    return Read(touch.primaryTouch.position.ReadValue(), press.wasPressedThisFrame, press.wasReleasedThisFrame, out position, out pressedThisFrame, out releasedThisFrame);
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (Time.unscaledTime - _lastTouchTime < EmulatedMouseWindow)
                {
                    return Read(mouse.position.ReadValue(), false, false, out position, out pressedThisFrame, out releasedThisFrame);
                }

                return Read(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, out position, out pressedThisFrame, out releasedThisFrame);
            }

            var pointer = Pointer.current;
            if (pointer != null)
            {
                return Read(pointer.position.ReadValue(), pointer.press.wasPressedThisFrame, pointer.press.wasReleasedThisFrame, out position, out pressedThisFrame, out releasedThisFrame);
            }

            position = default;
            pressedThisFrame = false;
            releasedThisFrame = false;
            return false;
        }

        public static bool IsOverUi(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null)
            {
                return false;
            }

            UiHits.Clear();
            system.RaycastAll(new PointerEventData(system) { position = position }, UiHits);
            return UiHits.Count > 0;
        }

        static bool Read(Vector2 pos, bool pressed, bool released, out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame)
        {
            position = pos;
            pressedThisFrame = pressed;
            releasedThisFrame = released;
            return true;
        }
    }
}
