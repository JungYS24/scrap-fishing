using UnityEngine;
using UnityEngine.InputSystem;

namespace ScrapFishing.Controls
{
    public static class PointerUtil
    {
        public static bool TryRead(out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame)
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                return Read(pointer.position.ReadValue(), pointer.press.wasPressedThisFrame, pointer.press.wasReleasedThisFrame, out position, out pressedThisFrame, out releasedThisFrame);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                return Read(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, out position, out pressedThisFrame, out releasedThisFrame);
            }

            var touch = Touchscreen.current;
            if (touch != null)
            {
                var primary = touch.primaryTouch;
                return Read(primary.position.ReadValue(), primary.press.wasPressedThisFrame, primary.press.wasReleasedThisFrame, out position, out pressedThisFrame, out releasedThisFrame);
            }

            position = default;
            pressedThisFrame = false;
            releasedThisFrame = false;
            return false;
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
