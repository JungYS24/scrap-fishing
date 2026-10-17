using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Boat
{
    [DefaultExecutionOrder(250)]
    public class DepthBubbles : MonoBehaviour
    {
        const int Count = 12;
        const float Speed = 0.7f;

        struct Bit
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float Drift;
            public float Phase;
        }

        Bit[] _bits;
        Camera _camera;
        CastingController _casting;
        Sprite _sprite;

        public void Bind(Camera camera, CastingController casting)
        {
            _camera = camera;
            _casting = casting;
            if (_bits == null)
            {
                Build();
            }
        }

        void LateUpdate()
        {
            if (_bits == null || _camera == null)
            {
                return;
            }

            var flow = Flow();
            var visible = Mathf.Abs(flow) > 0.01f;
            var halfH = _camera.orthographicSize;
            var halfW = halfH * GameView.Aspect;
            var origin = _camera.transform.position;
            for (var i = 0; i < _bits.Length; i++)
            {
                var bit = _bits[i];
                if (bit.Renderer == null)
                {
                    continue;
                }

                bit.Renderer.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                var pos = bit.Transform.position;
                pos.y += flow * Speed * Time.deltaTime;
                pos.x += Mathf.Sin(Time.time * 0.8f + bit.Phase) * bit.Drift * Time.deltaTime;
                if (pos.y > origin.y + halfH)
                {
                    pos.y = origin.y - halfH;
                }
                else if (pos.y < origin.y - halfH)
                {
                    pos.y = origin.y + halfH;
                }

                if (pos.x > origin.x + halfW)
                {
                    pos.x = origin.x - halfW;
                }
                else if (pos.x < origin.x - halfW)
                {
                    pos.x = origin.x + halfW;
                }

                bit.Transform.position = pos;
            }
        }

        float Flow()
        {
            if (_casting == null)
            {
                return 0f;
            }

            if (_casting.IsDescending)
            {
                return 1f;
            }

            if (_casting.IsReeling)
            {
                return -1f;
            }

            return 0f;
        }

        void Build()
        {
            _sprite = Circle();
            _bits = new Bit[Count];
            var halfH = _camera != null ? _camera.orthographicSize : GameView.OrthoSize;
            var halfW = halfH * GameView.Aspect;
            for (var i = 0; i < Count; i++)
            {
                var go = new GameObject("Bubble");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH), 0f);
                var scale = Random.Range(0.045f, 0.09f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprite;
                renderer.sortingOrder = 1;
                renderer.color = new Color(0.75f, 0.92f, 1f, Random.Range(0.18f, 0.34f));
                renderer.enabled = false;
                _bits[i] = new Bit
                {
                    Transform = go.transform,
                    Renderer = renderer,
                    Drift = Random.Range(-0.08f, 0.08f),
                    Phase = Random.Range(0f, 6.28f)
                };
            }
        }

        static Sprite Circle()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var edge = Mathf.Clamp01((0.92f - distance) / 0.22f);
                    var hole = Mathf.Clamp01((distance - 0.45f) / 0.2f);
                    var alpha = edge * Mathf.Lerp(0.15f, 1f, hole);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
