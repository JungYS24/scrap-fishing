using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Controls
{
    public class SwipeTrail : MonoBehaviour
    {
        LineRenderer _line;
        float _until;

        public void Build()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.startWidth = 0.08f;
            _line.endWidth = 0.03f;
            _line.sortingOrder = 12;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                _line.material = new Material(shader);
            }

            _line.startColor = new Color(0.22f, 1f, 0.61f, 0.95f);
            _line.endColor = new Color(0.35f, 0.85f, 1f, 0.2f);
            _line.enabled = false;
        }

        public void Show(SwipeInfo swipe)
        {
            if (_line == null)
            {
                return;
            }

            _line.SetPosition(0, swipe.StartWorld);
            _line.SetPosition(1, swipe.EndWorld);
            _line.enabled = true;
            _until = Time.unscaledTime + 0.28f;
        }

        void Update()
        {
            if (_line != null && _line.enabled && Time.unscaledTime >= _until)
            {
                _line.enabled = false;
            }
        }
    }
}
