using UnityEngine;

namespace ScrapFishing.Dive
{
    public class OxygenMeter : MonoBehaviour
    {
        public float Normalized { get; private set; }
        public bool IsFull => Normalized >= 1f;

        float _fillPerSecond;

        public void ResetMeter(float seconds)
        {
            Normalized = 0f;
            _fillPerSecond = seconds > 0.01f ? 1f / seconds : 1f;
        }

        public void Fill(float deltaTime)
        {
            Normalized = Mathf.Min(1f, Normalized + _fillPerSecond * deltaTime);
        }

        public void Add(float amount)
        {
            Normalized = Mathf.Min(1f, Normalized + amount);
        }
    }
}
