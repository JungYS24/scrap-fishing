using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Boat
{
    public class DepthGauge : MonoBehaviour
    {
        [SerializeField] float speed = 1.35f;
        [SerializeField] RectTransform marker;
        [SerializeField] Image markerImage;
        [SerializeField] RectTransform lockedArea;

        bool _locked;
        float _maxDepth = 1f;

        public float Normalized { get; private set; }

        public void SetMaxDepth(float maxDepth)
        {
            _maxDepth = Mathf.Clamp(maxDepth, 0.05f, 1f);
            Normalized = Mathf.Min(Normalized, _maxDepth);
            if (lockedArea != null)
            {
                lockedArea.anchorMin = Vector2.zero;
                lockedArea.anchorMax = new Vector2(1f, 1f - _maxDepth);
                lockedArea.gameObject.SetActive(_maxDepth < 1f);
            }
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            if (markerImage != null)
            {
                markerImage.color = locked ? Palette.Magenta : DepthZone.MarkerColor(Normalized);
            }
        }

        void Update()
        {
            if (_locked)
            {
                return;
            }

            Normalized = Mathf.PingPong(Time.time * speed, _maxDepth);
            if (marker != null)
            {
                var y = Mathf.Lerp(0.98f, 0.02f, Normalized);
                marker.anchorMin = new Vector2(0f, y);
                marker.anchorMax = new Vector2(1f, y);
                marker.anchoredPosition = Vector2.zero;
            }

            if (markerImage != null)
            {
                markerImage.color = DepthZone.MarkerColor(Normalized);
            }
        }
    }
}
