using UnityEngine;

namespace ScrapFishing.Dive
{
    public class Hazard : MonoBehaviour
    {
        [SerializeField] float radius = 0.42f;

        public bool Hits(Vector3 point)
        {
            return Vector3.Distance(transform.position, point) <= radius;
        }
    }
}
