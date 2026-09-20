using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class DepthGauge : MonoBehaviour
    {
        [SerializeField] float speed = 1.35f;

        Transform _marker;
        SpriteRenderer _markerRenderer;
        bool _locked;

        public float Normalized { get; private set; }

        public void Build(Transform parent)
        {
            transform.SetParent(parent, false);
            transform.position = new Vector3(1.85f, 3.55f, 0f);

            var track = new GameObject("Track").AddComponent<SpriteRenderer>();
            track.transform.SetParent(transform, false);
            track.sprite = PlaceholderFactory.Square(new Color(0.08f, 0.1f, 0.16f));
            track.transform.localScale = new Vector3(0.18f, 1.7f, 1f);
            track.sortingOrder = 10;

            _markerRenderer = new GameObject("Marker").AddComponent<SpriteRenderer>();
            _marker = _markerRenderer.transform;
            _marker.SetParent(transform, false);
            _markerRenderer.sprite = PlaceholderFactory.Square(Palette.NeonGreen);
            _marker.localScale = new Vector3(0.32f, 0.16f, 1f);
            _markerRenderer.sortingOrder = 11;
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            if (_markerRenderer != null)
            {
                _markerRenderer.color = locked ? Palette.Magenta : Palette.NeonGreen;
            }
        }

        void Update()
        {
            if (_locked)
            {
                return;
            }

            Normalized = Mathf.PingPong(Time.time * speed, 1f);
            if (_marker != null)
            {
                _marker.localPosition = new Vector3(0f, Mathf.Lerp(0.72f, -0.72f, Normalized), 0f);
            }
        }
    }
}
