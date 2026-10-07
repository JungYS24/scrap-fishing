using UnityEngine;

namespace ScrapFishing.Dive
{
    public class Hazard : MonoBehaviour
    {
        [SerializeField] float radius = 0.42f;
        [SerializeField] int amount = -25;

        public int Amount => amount;
        public int Size { get; private set; } = 1;

        public void Configure(int penalty, float hitRadius, int size)
        {
            amount = penalty;
            radius = hitRadius;
            Size = size;
        }

        public bool Hits(Vector3 point)
        {
            return Vector3.Distance(transform.position, point) <= radius;
        }
    }
}
