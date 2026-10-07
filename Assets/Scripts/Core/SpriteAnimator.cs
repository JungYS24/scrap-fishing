using UnityEngine;

namespace ScrapFishing.Core
{
    public class SpriteAnimator : MonoBehaviour
    {
        Sprite[] _frames;
        SpriteRenderer _renderer;
        float _fps;
        float _time;
        bool _loop;
        bool _destroyOnEnd;
        bool _faceMovement;
        float _lastX;

        public void Play(Sprite[] frames, float fps, bool loop, bool destroyOnEnd = false)
        {
            _frames = frames;
            _fps = fps;
            _loop = loop;
            _destroyOnEnd = destroyOnEnd;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            _time = loop && frames != null && frames.Length > 0 ? Random.value * frames.Length / fps : 0f;
            _lastX = transform.position.x;
            Apply(0);
        }

        public void FaceMovement(bool enabled)
        {
            _faceMovement = enabled;
            _lastX = transform.position.x;
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0)
            {
                return;
            }

            _time += Time.deltaTime;
            var index = Mathf.FloorToInt(_time * _fps);
            if (index >= _frames.Length)
            {
                if (_loop)
                {
                    index %= _frames.Length;
                }
                else if (_destroyOnEnd)
                {
                    Destroy(gameObject);
                    return;
                }
                else
                {
                    index = _frames.Length - 1;
                }
            }

            Apply(index);
            if (_faceMovement)
            {
                var x = transform.position.x;
                var dx = Mathf.Abs(x - _lastX);
                if (dx > 0.0005f && dx < 1f)
                {
                    _renderer.flipX = x < _lastX;
                }

                _lastX = x;
            }
        }

        void Apply(int index)
        {
            if (_renderer != null && _frames != null && _frames.Length > 0)
            {
                _renderer.sprite = _frames[Mathf.Clamp(index, 0, _frames.Length - 1)];
            }
        }
    }
}
