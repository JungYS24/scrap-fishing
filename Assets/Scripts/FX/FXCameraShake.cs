using UnityEngine;

namespace ScrapFishing.FX
{
    [DefaultExecutionOrder(50)]
    public class FXCameraShake : MonoBehaviour
    {
        float _remain;
        float _strength;

        public void Play(float strength, float duration)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            _strength = strength;
            _remain = duration;
        }

        void LateUpdate()
        {
            if (_remain <= 0f)
            {
                return;
            }

            _remain -= Time.unscaledDeltaTime;
            var mag = _strength * Mathf.Clamp01(_remain / 0.12f);
            transform.position += new Vector3(
                (Random.value * 2f - 1f) * mag,
                (Random.value * 2f - 1f) * mag,
                0f);
        }
    }
}
