using UnityEngine;

namespace ScrapFishing.Dive
{
    public class Hazard : MonoBehaviour
    {
        [SerializeField] float radius = 0.42f;
        [SerializeField] int amount = -25;

        public int Amount => amount;

        public void SetAmount(int value)
        {
            amount = value;
        }

        public bool Hits(Vector3 point)
        {
            return Vector3.Distance(transform.position, point) <= radius;
        }
    }
}
