using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Controls
{
    public class SwipeTrail : MonoBehaviour
    {
        [SerializeField] int maxPoints = 10;
        [SerializeField] float trailSeconds = 0.16f;

        readonly List<Vector3> _points = new List<Vector3>();
        LineRenderer _line;
        float _until;

        public void Build()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = 0;
            _line.useWorldSpace = true;
            _line.startWidth = 0.03f;
            _line.endWidth = 0.08f;
            _line.sortingOrder = 12;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                _line.material = new Material(shader);
            }

            _line.startColor = new Color(0.35f, 0.85f, 1f, 0.2f);
            _line.endColor = new Color(0.22f, 1f, 0.61f, 0.95f);
            _line.enabled = false;
        }

        public void Begin()
        {
            _points.Clear();
            if (_line == null)
            {
                return;
            }

            _line.positionCount = 0;
            _line.enabled = false;
        }

        public void Extend(SwipeInfo segment)
        {
            if (_line == null)
            {
                return;
            }

            if (_points.Count == 0)
            {
                _points.Add(segment.StartWorld);
            }

            _points.Add(segment.EndWorld);
            var limit = Mathf.Max(2, maxPoints);
            while (_points.Count > limit)
            {
                _points.RemoveAt(0);
            }

            _line.positionCount = _points.Count;
            _line.SetPositions(_points.ToArray());
            _line.enabled = true;
            _until = Time.unscaledTime + Mathf.Max(0.02f, trailSeconds);
        }

        void Update()
        {
            if (_line != null && _line.enabled && Time.unscaledTime >= _until)
            {
                _line.enabled = false;
                _points.Clear();
            }
        }
    }
}
