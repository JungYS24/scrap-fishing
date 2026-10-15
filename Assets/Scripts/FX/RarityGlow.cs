using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Data;
using ScrapFishing.Scrap;
using ScrapFishing.UI;
using UnityEngine;

namespace ScrapFishing.FX
{
    [DefaultExecutionOrder(100)]
    public class RarityGlow : MonoBehaviour
    {
        const float AttachedStrength = 0.7f;
        const int SpriteSize = 64;

        struct Style
        {
            public Color Color;
            public float ScaleMin;
            public float ScaleMax;
            public float AlphaMin;
            public float AlphaMax;
            public float Period;
            public float SparkleInterval;
            public int SparkleMin;
            public int SparkleMax;
            public int SparklePool;
            public bool Rays;
            public bool IntroFlash;
        }

        static readonly Style RareStyle = new Style
        {
            Color = new Color(0x3F / 255f, 0xA9 / 255f, 0xFF / 255f, 1f),
            ScaleMin = 1.35f,
            ScaleMax = 1.5f,
            AlphaMin = 0.25f,
            AlphaMax = 0.5f,
            Period = 1.4f,
            SparkleInterval = 0f,
            SparkleMin = 0,
            SparkleMax = 0,
            SparklePool = 0
        };

        static readonly Style EpicStyle = new Style
        {
            Color = new Color(0xB4 / 255f, 0x5C / 255f, 0xFF / 255f, 1f),
            ScaleMin = 1.5f,
            ScaleMax = 1.75f,
            AlphaMin = 0.35f,
            AlphaMax = 0.7f,
            Period = 1f,
            SparkleInterval = 0.7f,
            SparkleMin = 1,
            SparkleMax = 1,
            SparklePool = 2
        };

        static readonly Style LegendStyle = new Style
        {
            Color = new Color(0xFF / 255f, 0x9A / 255f, 0x2E / 255f, 1f),
            ScaleMin = 1.7f,
            ScaleMax = 2.1f,
            AlphaMin = 0.45f,
            AlphaMax = 0.9f,
            Period = 0.7f,
            SparkleInterval = 0.35f,
            SparkleMin = 1,
            SparkleMax = 2,
            SparklePool = 6,
            Rays = true,
            IntroFlash = true
        };

        static Sprite _circle;
        static Sprite _star;
        static Sprite _ray;
        static Material _spriteMaterial;
        static UiFxSettings _uiFx;

        ScrapView _view;
        Style _style;
        bool _ready;
        float _pulse;
        float _fit = 1f;
        float _sparkleTimer;
        float _raySpeed;
        int _rayCount;
        bool _introPlayed;
        Tween _pulseTween;
        Tween _introTween;
        SpriteRenderer _glow;
        SpriteRenderer _flash;
        Transform _rayRoot;
        readonly List<SpriteRenderer> _rays = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _sparkles = new List<SpriteRenderer>();

        public static void Apply(ScrapView view)
        {
            if (view == null)
            {
                return;
            }

            var existing = view.transform.Find("RarityGlow");
            if (!TryStyle(view, out var style))
            {
                if (existing != null)
                {
                    var idle = existing.GetComponent<RarityGlow>();
                    if (idle != null)
                    {
                        idle.Hide();
                    }
                    else
                    {
                        existing.gameObject.SetActive(false);
                    }
                }

                return;
            }

            RarityGlow glow;
            if (existing == null)
            {
                var go = new GameObject("RarityGlow");
                go.transform.SetParent(view.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.hideFlags = HideFlags.DontSave;
                glow = go.AddComponent<RarityGlow>();
            }
            else
            {
                if (!existing.gameObject.activeSelf)
                {
                    existing.gameObject.SetActive(true);
                }

                glow = existing.GetComponent<RarityGlow>();
                if (glow == null)
                {
                    glow = existing.gameObject.AddComponent<RarityGlow>();
                }
            }

            glow.Configure(view, style);
        }

        static bool TryStyle(ScrapView view, out Style style)
        {
            style = default;
            if (view == null || view.data == null || view.ChipValue < 0)
            {
                return false;
            }

            switch (view.data.Rarity)
            {
                case ItemRarity.Rare:
                    style = RareStyle;
                    return true;
                case ItemRarity.Epic:
                    style = EpicStyle;
                    return true;
                case ItemRarity.Legend:
                    style = LegendStyle;
                    return true;
                default:
                    return false;
            }
        }

        void Configure(ScrapView view, Style style)
        {
            _view = view;
            _style = style;
            _ready = true;
            _fit = FitOf(view);
            _raySpeed = Random.Range(16f, 32f) * (Random.value < 0.5f ? -1f : 1f);
            _rayCount = Random.Range(4, 7);
            _sparkleTimer = Random.Range(0f, Mathf.Max(0.05f, style.SparkleInterval));
            _introPlayed = false;
            EnsureGlow();
            EnsureRays();
            EnsureSparkles();
            EnsureFlash();
            Restart();
        }

        void Hide()
        {
            _ready = false;
            KillTweens();
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_ready)
            {
                Restart();
            }
        }

        void OnDisable()
        {
            KillTweens();
        }

        void OnDestroy()
        {
            KillTweens();
        }

        void Restart()
        {
            KillTweens();
            _pulse = 0f;
            _pulseTween = DOTween.To(() => _pulse, value => _pulse = value, 1f, _style.Period)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Random.Range(0f, _style.Period));
            _pulseTween.SetLink(gameObject);
            if (_style.IntroFlash && !_introPlayed)
            {
                _introPlayed = true;
                PlayIntro();
            }
        }

        void PlayIntro()
        {
            if (_flash == null)
            {
                return;
            }

            _flash.gameObject.SetActive(true);
            _flash.color = new Color(1f, 0.92f, 0.75f, 0.95f);
            _flash.transform.localScale = Vector3.one * (_fit * _style.ScaleMax * 1.15f);
            var intro = DOTween.Sequence();
            intro.SetLink(gameObject);
            intro.Append(_flash.transform.DOScale(_fit * _style.ScaleMax * 2.4f, 0.28f).SetEase(Ease.OutQuad));
            intro.Join(_flash.DOFade(0f, 0.28f));
            _introTween = intro;
            intro.OnComplete(() =>
            {
                if (_flash != null)
                {
                    _flash.gameObject.SetActive(false);
                }
            });
        }

        void Update()
        {
            if (!_ready)
            {
                return;
            }

            if (_rayRoot != null && _rayRoot.gameObject.activeSelf)
            {
                _rayRoot.Rotate(0f, 0f, _raySpeed * Time.deltaTime);
            }

            if (_style.SparkleInterval <= 0f || _style.SparkleMax <= 0)
            {
                return;
            }

            _sparkleTimer -= Time.deltaTime;
            if (_sparkleTimer > 0f)
            {
                return;
            }

            var interval = _style.SparkleInterval;
            if (Reduced)
            {
                interval *= 2f;
            }

            _sparkleTimer = interval;
            var count = Random.Range(_style.SparkleMin, _style.SparkleMax + 1);
            for (var i = 0; i < count; i++)
            {
                PopSparkle();
            }
        }

        void LateUpdate()
        {
            if (!_ready || _view == null)
            {
                return;
            }

            var scrap = _view.Renderer;
            if (scrap == null)
            {
                return;
            }

            _fit = FitOf(_view);
            var strength = scrap.color.a * (_view.Attached ? AttachedStrength : 1f);
            var alpha = Mathf.Lerp(_style.AlphaMin, _style.AlphaMax, _pulse) * strength;
            var scale = _fit * Mathf.Lerp(_style.ScaleMin, _style.ScaleMax, _pulse);
            var order = scrap.sortingOrder;
            var layer = scrap.sortingLayerID;
            Paint(_glow, layer, order - 1, _style.Color, alpha, scale);
            if (_style.Rays)
            {
                var length = Mathf.Max(0.2f, _fit * _style.ScaleMax * 1.15f);
                var width = Mathf.Max(0.04f, _fit * 0.16f);
                var rayColor = _style.Color;
                rayColor.a = alpha * 0.85f;
                for (var i = 0; i < _rays.Count; i++)
                {
                    var ray = _rays[i];
                    if (ray == null || !ray.gameObject.activeSelf)
                    {
                        continue;
                    }

                    ray.sortingLayerID = layer;
                    ray.sortingOrder = order - 2;
                    ray.color = rayColor;
                    ray.transform.localScale = new Vector3(width, length, 1f);
                }
            }

            if (_flash != null && _flash.gameObject.activeSelf)
            {
                _flash.sortingLayerID = layer;
                _flash.sortingOrder = order - 1;
            }

            for (var i = 0; i < _sparkles.Count; i++)
            {
                var spark = _sparkles[i];
                if (spark == null || !spark.gameObject.activeSelf)
                {
                    continue;
                }

                spark.sortingLayerID = layer;
                spark.sortingOrder = order + 1;
                var color = _style.Color;
                color.a = strength;
                spark.color = color;
            }
        }

        void PopSparkle()
        {
            SpriteRenderer spark = null;
            for (var i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null && !_sparkles[i].gameObject.activeSelf)
                {
                    spark = _sparkles[i];
                    break;
                }
            }

            if (spark == null)
            {
                return;
            }

            var size = ScrapSize(_view);
            var reach = Mathf.Max(size.x, size.y);
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var radius = Random.Range(0.15f, 0.5f) * reach;
            var peak = reach * 0.22f / Mathf.Max(0.001f, StarSprite().bounds.size.x);
            spark.gameObject.SetActive(true);
            spark.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            spark.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            spark.transform.localScale = Vector3.zero;
            spark.transform.DOKill(false);
            var body = spark.transform;
            var seq = DOTween.Sequence();
            seq.SetLink(spark.gameObject);
            seq.Append(body.DOScale(peak, 0.16f).SetEase(Ease.OutQuad));
            seq.Append(body.DOScale(0f, 0.2f).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                if (spark != null)
                {
                    spark.gameObject.SetActive(false);
                }
            });
        }

        void EnsureGlow()
        {
            if (_glow != null)
            {
                return;
            }

            _glow = CreateRenderer("Glow", CircleSprite());
        }

        void EnsureFlash()
        {
            if (!_style.IntroFlash)
            {
                if (_flash != null)
                {
                    _flash.gameObject.SetActive(false);
                }

                return;
            }

            if (_flash == null)
            {
                _flash = CreateRenderer("Flash", CircleSprite());
            }

            _flash.gameObject.SetActive(false);
        }

        void EnsureRays()
        {
            if (!_style.Rays)
            {
                if (_rayRoot != null)
                {
                    _rayRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (_rayRoot == null)
            {
                var root = new GameObject("Rays");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                root.hideFlags = HideFlags.DontSave;
                _rayRoot = root.transform;
            }

            _rayRoot.gameObject.SetActive(true);
            _rayRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            while (_rays.Count < _rayCount)
            {
                var ray = CreateRenderer("Ray", RaySprite(), _rayRoot);
                _rays.Add(ray);
            }

            var length = Mathf.Max(0.2f, _fit * _style.ScaleMax * 1.15f);
            var width = Mathf.Max(0.05f, _fit * 0.16f);
            for (var i = 0; i < _rays.Count; i++)
            {
                var active = i < _rayCount;
                _rays[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                _rays[i].transform.localRotation = Quaternion.Euler(0f, 0f, i * (360f / _rayCount));
                _rays[i].transform.localScale = new Vector3(width, length, 1f);
                _rays[i].color = _style.Color;
            }
        }

        void EnsureSparkles()
        {
            var pool = _style.SparklePool;
            while (_sparkles.Count < pool)
            {
                var spark = CreateRenderer("Sparkle", StarSprite());
                spark.gameObject.SetActive(false);
                _sparkles.Add(spark);
            }

            for (var i = pool; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkles[i].transform.DOKill(false);
                    _sparkles[i].gameObject.SetActive(false);
                }
            }
        }

        SpriteRenderer CreateRenderer(string name, Sprite sprite, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.hideFlags = HideFlags.DontSave;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var material = SpriteMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return renderer;
        }

        void KillTweens()
        {
            if (_pulseTween != null)
            {
                _pulseTween.Kill();
                _pulseTween = null;
            }

            if (_introTween != null)
            {
                _introTween.Kill();
                _introTween = null;
            }

            if (_flash != null)
            {
                _flash.transform.DOKill(false);
                _flash.DOKill(false);
                _flash.gameObject.SetActive(false);
            }

            for (var i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] == null)
                {
                    continue;
                }

                _sparkles[i].transform.DOKill(false);
                _sparkles[i].gameObject.SetActive(false);
            }
        }

        static void Paint(SpriteRenderer renderer, int layer, int order, Color color, float alpha, float uniformScale)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order;
            color.a = alpha;
            renderer.color = color;
            if (uniformScale > 0f)
            {
                renderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            }
        }

        static float FitOf(ScrapView view)
        {
            var size = ScrapSize(view);
            var glow = Mathf.Max(0.001f, CircleSprite().bounds.size.x);
            return Mathf.Max(size.x, size.y) / glow;
        }

        static Vector2 ScrapSize(ScrapView view)
        {
            var sprite = view != null && view.Renderer != null ? view.Renderer.sprite : null;
            if (sprite == null)
            {
                return Vector2.one;
            }

            return sprite.bounds.size;
        }

        static bool Reduced
        {
            get
            {
                if (_uiFx == null)
                {
                    _uiFx = Resources.Load<UiFxSettings>("UiFxSettings");
                }

                return _uiFx != null && _uiFx.Reduced;
            }
        }

        static Material SpriteMaterial()
        {
            if (_spriteMaterial != null)
            {
                return _spriteMaterial;
            }

            var probe = new GameObject("RarityGlowMaterial")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var renderer = probe.AddComponent<SpriteRenderer>();
            _spriteMaterial = renderer.sharedMaterial;
            if (Application.isPlaying)
            {
                Destroy(probe);
            }
            else
            {
                DestroyImmediate(probe);
            }

            return _spriteMaterial;
        }

        static Sprite CircleSprite()
        {
            if (_circle == null)
            {
                _circle = MakeSprite("RarityGlowCircle", SpriteSize, SpriteSize, 0.5f, 0.5f, SpriteSize, PaintCircle);
            }

            return _circle;
        }

        static Sprite StarSprite()
        {
            if (_star == null)
            {
                _star = MakeSprite("RarityGlowStar", 32, 32, 0.5f, 0.5f, 32f, PaintStar);
            }

            return _star;
        }

        static Sprite RaySprite()
        {
            if (_ray == null)
            {
                _ray = MakeSprite("RarityGlowRay", 24, 64, 0.5f, 0f, 64f, PaintRay);
            }

            return _ray;
        }

        static Sprite MakeSprite(string name, int width, int height, float pivotX, float pivotY, float pixelsPerUnit, System.Action<Color32[], int, int> paint)
        {
            var pixels = new Color32[width * height];
            paint(pixels, width, height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(pivotX, pivotY), pixelsPerUnit);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void PaintCircle(Color32[] pixels, int width, int height)
        {
            var center = (width - 1) * 0.5f;
            var radius = center;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
        }

        static void PaintStar(Color32[] pixels, int width, int height)
        {
            var center = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = Mathf.Abs(x - center) / center;
                    var dy = Mathf.Abs(y - center) / center;
                    var cross = Mathf.Min(dx, dy);
                    var arm = Mathf.Min(dx, dy * 0.22f) + Mathf.Min(dy, dx * 0.22f);
                    var alpha = Mathf.Clamp01(1f - arm * 1.35f);
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1f - cross * 4.5f) * 0.35f);
                    alpha *= alpha;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
        }

        static void PaintRay(Color32[] pixels, int width, int height)
        {
            var half = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                var along = height <= 1 ? 0f : y / (float)(height - 1);
                var fade = Mathf.Clamp01(1f - along);
                fade *= fade;
                for (var x = 0; x < width; x++)
                {
                    var across = Mathf.Abs(x - half) / half;
                    var side = Mathf.Clamp01(1f - across);
                    side *= side;
                    var alpha = fade * side;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
        }
    }
}
