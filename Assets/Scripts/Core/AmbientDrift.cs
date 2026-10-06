using UnityEngine;

namespace ScrapFishing.Core
{
    public class AmbientDrift : MonoBehaviour
    {
        Vector3 _origin;
        float _rangeX;
        float _rangeY;
        float _speed;
        float _phase;

        public void Configure(Vector3 origin, float rangeX, float rangeY, float speed)
        {
            _origin = origin;
            _rangeX = rangeX;
            _rangeY = rangeY;
            _speed = speed;
            _phase = origin.x * 1.7f + origin.y * 0.6f;
            enabled = true;
        }

        public void PullOrigin(Vector3 target, float distance)
        {
            _origin = Vector3.MoveTowards(_origin, target, distance);
        }

        void Update()
        {
            var t = Time.time * _speed + _phase;
            transform.position = _origin + new Vector3(Mathf.Sin(t) * _rangeX, Mathf.Cos(t * 0.73f) * _rangeY, 0f);
        }
    }
}
