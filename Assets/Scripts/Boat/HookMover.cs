using UnityEngine;

namespace ScrapFishing.Boat
{
    public class HookMover : MonoBehaviour
    {
        public Vector3 Position => transform.position;

        LineRenderer _line;
        Vector3 _surfacePoint;

        public void Build(Vector3 surfacePoint)
        {
            _surfacePoint = surfacePoint;
            transform.position = surfacePoint;

            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.startWidth = 0.035f;
            _line.endWidth = 0.02f;
            _line.sortingOrder = 3;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                _line.material = new Material(shader);
            }
            _line.startColor = new Color(0.22f, 1f, 0.61f, 0.9f);
            _line.endColor = new Color(0.22f, 1f, 0.61f, 0.55f);
            RefreshLine();
        }

        public void Place(Vector3 position)
        {
            transform.position = position;
            RefreshLine();
        }

        void RefreshLine()
        {
            if (_line == null)
            {
                return;
            }

            _line.SetPosition(0, _surfacePoint);
            _line.SetPosition(1, transform.position);
        }
    }
}
