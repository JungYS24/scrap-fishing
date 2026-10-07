using ScrapFishing.Controls;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Dive
{
    public class DiverController : MonoBehaviour
    {
        [SerializeField] float speed = 3.4f;
        [SerializeField] float ascentSpeed = 3.2f;

        VirtualJoystick _stick;
        Vector2 _min;
        Vector2 _max;
        SpriteRenderer _renderer;
        bool _bound;
        bool _forcedAscent;
        float _hurtUntil;
        Color _baseColor = Color.white;
        Sprite[] _idle;
        Sprite[] _swim;
        Sprite[] _hurt;

        public Vector3 Position => transform.position;
        public bool IsForcedAscent => _forcedAscent;

        public void Bind(VirtualJoystick stick, Vector2 min, Vector2 max)
        {
            _stick = stick;
            _min = min;
            _max = max;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
            }

            _bound = true;
        }

        public void SetAnimations(Sprite[] idle, Sprite[] swim, Sprite[] hurt)
        {
            _idle = idle;
            _swim = swim;
            _hurt = hurt;
        }

        public void Place(Vector3 position)
        {
            transform.position = position;
            _forcedAscent = false;
            _hurtUntil = 0f;
            if (_renderer != null)
            {
                _renderer.color = _baseColor;
            }
        }

        public void FlashHurt()
        {
            _hurtUntil = Time.time + 0.36f;
            if (_renderer != null)
            {
                _renderer.color = Palette.Magenta;
            }
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
            if (!_forcedAscent && move.sqrMagnitude < 0.00001f)
            {
                next.y += Mathf.Sin(Time.time * 2.2f) * 0.012f;
            }

            transform.position = next;
            if (_renderer != null)
            {
                if (Time.time >= _hurtUntil && _renderer.color != _baseColor)
                {
                    _renderer.color = _baseColor;
                }

                if (Mathf.Abs(move.x) > 0.0001f)
                {
                    _renderer.flipX = move.x < 0f;
                }

                Animate(_forcedAscent || move.sqrMagnitude > 0.000004f);
            }
        }

        void Animate(bool moving)
        {
            var hurt = Time.time < _hurtUntil;
            var frames = hurt ? _hurt : moving ? _swim : _idle;
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            var fps = hurt ? 14f : moving ? 10f : 6f;
            _renderer.sprite = frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        }
    }
}
