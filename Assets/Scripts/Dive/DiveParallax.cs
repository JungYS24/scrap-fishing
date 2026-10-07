using UnityEngine;

namespace ScrapFishing.Dive
{
    public class DiveParallax : MonoBehaviour
    {
        static readonly Vector2 Center = new Vector2(0f, -0.4f);

        [SerializeField] float factor = 0.05f;

        Transform _target;
        Vector3 _origin;

        public void Bind(Transform target)
        {
            _target = target;
            _origin = transform.localPosition;
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            var offset = (Vector2)_target.position - Center;
            transform.localPosition = _origin - (Vector3)(offset * factor);
        }
    }
}
