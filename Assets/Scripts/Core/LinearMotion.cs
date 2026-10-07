using UnityEngine;

namespace ScrapFishing.Core
{
    public class LinearMotion : MonoBehaviour
    {
        Vector3 _velocity;
        float _wobble;
        float _wobbleSpeed;
        float _wrapX;
        float _phase;
        float _baseY;
        float _baseX;

        public void Configure(Vector2 velocity, float wobble, float wobbleSpeed, float wrapX = 0f)
        {
            _velocity = velocity;
            _wobble = wobble;
            _wobbleSpeed = wobbleSpeed;
            _wrapX = wrapX;
            _phase = Random.value * Mathf.PI * 2f;
            _baseX = transform.position.x;
            _baseY = transform.position.y;
        }

        void Update()
        {
            _baseX += _velocity.x * Time.deltaTime;
            _baseY += _velocity.y * Time.deltaTime;
            if (_wrapX > 0f)
            {
                if (_baseX > _wrapX) _baseX = -_wrapX;
                else if (_baseX < -_wrapX) _baseX = _wrapX;
            }

            var sway = Mathf.Sin(Time.time * _wobbleSpeed + _phase) * _wobble;
            var horizontal = Mathf.Abs(_velocity.x) >= Mathf.Abs(_velocity.y);
            transform.position = horizontal
                ? new Vector3(_baseX, _baseY + sway, 0f)
                : new Vector3(_baseX + sway, _baseY, 0f);
        }
    }
}
