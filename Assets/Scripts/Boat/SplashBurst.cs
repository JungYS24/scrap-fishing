using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class SplashBurst : MonoBehaviour
    {
        const int DropCount = 7;
        const float Lifetime = 0.38f;
        const float Gravity = 9f;
        const int SortingOrder = 7;

        static Sprite _dot;

        readonly Transform[] _drops = new Transform[DropCount];
        readonly Vector3[] _velocity = new Vector3[DropCount];
        readonly SpriteRenderer[] _renderers = new SpriteRenderer[DropCount + 1];
        Transform _ring;
        float _age;

        public static void Spawn(Vector3 at)
        {
            if (_dot == null)
            {
                _dot = PlaceholderFactory.Circle(Color.white, 16);
            }

            var go = new GameObject("Splash");
            go.transform.position = at;
            go.AddComponent<SplashBurst>().Build();
        }

        void Build()
        {
            var color = Color.Lerp(Palette.Cyan, Color.white, 0.4f);
            for (var i = 0; i < DropCount; i++)
            {
                var angle = Mathf.Lerp(35f, 145f, i / (DropCount - 1f)) + Random.Range(-8f, 8f);
                var speed = Random.Range(1.6f, 2.6f);
                _velocity[i] = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * speed;
                _drops[i] = CreateDot("Drop", Vector3.one * Random.Range(0.06f, 0.1f), color, i);
            }

            _ring = CreateDot("Ring", new Vector3(0.2f, 0.05f, 1f), color, DropCount);
        }

        Transform CreateDot(string name, Vector3 scale, Color color, int index)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _dot;
            renderer.color = color;
            renderer.sortingOrder = SortingOrder;
            _renderers[index] = renderer;
            return go.transform;
        }

        void Update()
        {
            _age += Time.deltaTime;
            var t = _age / Lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            for (var i = 0; i < DropCount; i++)
            {
                _velocity[i].y -= Gravity * Time.deltaTime;
                _drops[i].localPosition += _velocity[i] * Time.deltaTime;
            }

            _ring.localScale = new Vector3(Mathf.Lerp(0.2f, 0.9f, t), Mathf.Lerp(0.05f, 0.12f, t), 1f);
            var alpha = 1f - t * t;
            foreach (var renderer in _renderers)
            {
                var c = renderer.color;
                c.a = alpha;
                renderer.color = c;
            }
        }
    }
}
