using ScrapFishing.Controls;
using UnityEngine;

namespace ScrapFishing.Dive
{
    public class DiverController : MonoBehaviour
    {
        [SerializeField] float speed = 3.4f;
        [SerializeField] float ascentSpeed = 4.6f;

        VirtualJoystick _stick;
        Vector2 _min;
        Vector2 _max;
        SpriteRenderer _renderer;
        bool _bound;
        bool _forcedAscent;

        public Vector3 Position => transform.position;
        public bool IsForcedAscent => _forcedAscent;

        public void Bind(VirtualJoystick stick, Vector2 min, Vector2 max)
        {
            _stick = stick;
            _min = min;
            _max = max;
            _renderer = GetComponent<SpriteRenderer>();
            _bound = true;
        }

        public void Place(Vector3 position)
        {
            transform.position = position;
            _forcedAscent = false;
        }

        public void BeginForcedAscent()
        {
            _forcedAscent = true;
        }

        void Update()
        {
            if (!_bound || !isActiveAndEnabled)
            {
                return;
            }

            Vector3 move;
            if (_forcedAscent)
            {
                move = Vector3.up * ascentSpeed * Time.deltaTime;
            }
            else if (_stick != null)
            {
                move = (Vector3)_stick.Value * speed * Time.deltaTime;
            }
            else
            {
                return;
            }

            var next = transform.position + move;
            next.x = Mathf.Clamp(next.x, _min.x, _max.x);
            next.y = Mathf.Clamp(next.y, _min.y, _max.y);
            transform.position = next;
            if (_renderer != null && Mathf.Abs(move.x) > 0.0001f)
            {
                _renderer.flipX = move.x < 0f;
            }
        }
    }
}
