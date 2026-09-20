using UnityEngine;

namespace ScrapFishing.Controls
{
    public class VirtualJoystick : MonoBehaviour
    {
        public Vector2 Value { get; private set; }

        public void SetValue(Vector2 value)
        {
            Value = Vector2.ClampMagnitude(value, 1f);
        }

        public void Release()
        {
            Value = Vector2.zero;
        }
    }
}
