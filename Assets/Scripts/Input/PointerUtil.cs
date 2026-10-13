using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ScrapFishing.Controls
{
    public static class PointerUtil
    {
        const float EmulatedMouseWindow = 0.6f;
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
        static readonly StringBuilder Names = new StringBuilder();
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
            RaycastUi(position);
            for (var i = 0; i < UiHits.Count; i++)
            {
                if (IsClickable(UiHits[i].gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        public static string HitNames(Vector2 position)
        {
            RaycastUi(position);
            Names.Length = 0;
            for (var i = 0; i < UiHits.Count; i++)
            {
                if (i > 0)
                {
                    Names.Append(", ");
                }

                var go = UiHits[i].gameObject;
                Names.Append(go != null ? go.name : "?");
            }

            return Names.ToString();
        }

        static void RaycastUi(Vector2 position)
        {
            UiHits.Clear();
            var system = EventSystem.current;
            if (system == null)
            {
                return;
            }

            system.RaycastAll(new PointerEventData(system) { position = position }, UiHits);
        }

        static bool IsClickable(GameObject go)
        {
            var t = go != null ? go.transform : null;
            while (t != null)
            {
                if (t.GetComponent<Selectable>() != null || t.GetComponent<IPointerClickHandler>() != null)
                {
                    return true;
                }

                t = t.parent;
            }

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
