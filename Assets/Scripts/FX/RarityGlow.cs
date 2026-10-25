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
            public bool Mark;
            public Color[] Cycle;
            public float CycleStep;
            public bool Crystal;
        }

        // 싱크로: 하양·핑크·연두가 번갈아 빛나는 크리스탈.
        public static readonly Color[] SynchroColors =
        {
            Color.white,
            new Color(0xFF / 255f, 0x5F / 255f, 0xD2 / 255f, 1f),
            new Color(0xB8 / 255f, 0xFF / 255f, 0x3C / 255f, 1f)
        };

        static readonly Style HazardStyle = new Style
        {
            Color = new Color(1f, 0.23137255f, 0.23137255f, 1f),
            ScaleMin = 1.28f,
            ScaleMax = 1.48f,
            AlphaMin = 0.4f,
            AlphaMax = 0.85f,
            Period = 0.55f,
            Mark = true
        };

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

        static readonly Style SynchroStyle = new Style
        {
            Color = Color.white,
            ScaleMin = 1.8f,
            ScaleMax = 2.3f,
            AlphaMin = 0.5f,
            AlphaMax = 0.95f,
            Period = 0.6f,
            SparkleInterval = 0.25f,
            SparkleMin = 1,
            SparkleMax = 2,
            SparklePool = 8,
            Rays = true,
            IntroFlash = true,
            Cycle = SynchroColors,
            CycleStep = 0.45f,
            Crystal = true
        };

        const int ShardCount = 6;

        static Sprite _circle;
        static Sprite _shard;
        static Sprite _star;
        static Sprite _ray;
        static Sprite _bang;
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
        SpriteRenderer _mark;
        SpriteRenderer _flash;
        Transform _rayRoot;
        Transform _shardRoot;
        readonly List<SpriteRenderer> _shards = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _rays = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _sparkles = new List<SpriteRenderer>();

        public static Color BorderColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return RareStyle.Color;
                case ItemRarity.Epic: return EpicStyle.Color;
                case ItemRarity.Legend: return LegendStyle.Color;
                case ItemRarity.Synchro: return SynchroStyle.Color;
                default: return new Color(0.604f, 0.627f, 0.651f, 1f);
            }
        }

        public static Color HazardBorder => HazardStyle.Color;

        public static void PlayAcquireFlash(ScrapView view)
        {
            if (view == null || view.data == null || view.ChipValue < 0)
            {
                return;
            }

            if (view.data.Rarity == ItemRarity.Epic)
            {
                Fx.ScreenFlash(EpicStyle.Color, 0.12f);
                return;
            }

            if (view.data.Rarity == ItemRarity.Legend)
            {
                Fx.ScreenFlash(LegendStyle.Color, 0.16f);
                Fx.ShakeCamera(0.05f, 0.12f);
                return;
            }

            if (view.data.Rarity == ItemRarity.Synchro)
            {
                PlaySynchroFlash();
            }
        }

        // 레전드보다 진하고 길게: 하양 → 핑크 → 연두로 이어서 번쩍이고 화면을 더 크게 흔든다.
        static void PlaySynchroFlash()
        {
            Fx.ScreenFlash(SynchroColors[0], 0.3f, 0.85f);
            Fx.ShakeCamera(0.09f, 0.25f);
            for (var i = 1; i < SynchroColors.Length; i++)
            {
                var color = SynchroColors[i];
                var alpha = 0.75f - i * 0.08f;
                DOVirtual.DelayedCall(i * 0.12f, () => Fx.ScreenFlash(color, 0.3f, alpha)).SetUpdate(true);
            }
        }

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
            if (view == null)
            {
                return false;
            }

            if (view.ChipValue < 0)
            {
                style = HazardStyle;
                return true;
            }

            if (view.data == null)
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
                case ItemRarity.Synchro:
                    style = SynchroStyle;
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
            EnsureMark();
            EnsureRays();
            EnsureShards();
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

            if (_shardRoot != null && _shardRoot.gameObject.activeSelf)
            {
                _shardRoot.Rotate(0f, 0f, -_raySpeed * 1.6f * Time.deltaTime);
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
            Paint(_glow, layer, order - 1, ColorAt(0f), alpha, scale);
            if (_mark != null && _mark.gameObject.activeSelf)
            {
                var mark = _style.Color;
                mark.a = Mathf.Clamp01(0.85f * strength + 0.15f);
                Paint(_mark, layer, order + 2, mark, mark.a, _fit * 0.42f);
                _mark.transform.localPosition = new Vector3(_fit * 0.28f, _fit * 0.32f, 0f);
            }
            if (_style.Rays)
            {
                var length = Mathf.Max(0.2f, _fit * _style.ScaleMax * 1.15f);
                var width = Mathf.Max(0.04f, _fit * 0.16f);
                for (var i = 0; i < _rays.Count; i++)
                {
                    var ray = _rays[i];
                    if (ray == null || !ray.gameObject.activeSelf)
                    {
                        continue;
                    }

                    var rayColor = ColorAt(i);
                    rayColor.a = alpha * 0.85f;
                    ray.sortingLayerID = layer;
                    ray.sortingOrder = order - 2;
                    ray.color = rayColor;
                    ray.transform.localScale = new Vector3(width, length, 1f);
                }
            }

            if (_style.Crystal)
            {
                var ring = _fit * 0.8f;
                var shardScale = _fit * Mathf.Lerp(0.2f, 0.26f, _pulse);
                for (var i = 0; i < _shards.Count; i++)
                {
                    var shard = _shards[i];
                    if (shard == null || !shard.gameObject.activeSelf)
                    {
                        continue;
                    }

                    var angle = i * (360f / _shards.Count);
                    var radians = angle * Mathf.Deg2Rad;
                    shard.transform.localPosition = new Vector3(Mathf.Cos(radians) * ring, Mathf.Sin(radians) * ring, 0f);
                    shard.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                    Paint(shard, layer, order + 1, ColorAt(i + 0.5f), Mathf.Clamp01(alpha * 1.1f), shardScale);
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
                var color = ColorAt(i * 0.7f);
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

        void EnsureMark()
        {
            if (!_style.Mark)
            {
                if (_mark != null)
                {
                    _mark.gameObject.SetActive(false);
                }

                return;
            }

            if (_mark == null)
            {
                _mark = CreateRenderer("HazardMark", BangSprite());
            }

            _mark.gameObject.SetActive(true);
            _mark.color = _style.Color;
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

        void EnsureShards()
        {
            if (!_style.Crystal)
            {
                if (_shardRoot != null)
                {
                    _shardRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (_shardRoot == null)
            {
                var root = new GameObject("Shards");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                root.hideFlags = HideFlags.DontSave;
                _shardRoot = root.transform;
            }

            _shardRoot.gameObject.SetActive(true);
            _shardRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            while (_shards.Count < ShardCount)
            {
                _shards.Add(CreateRenderer("Shard", ShardSprite(), _shardRoot));
            }
        }

        // 순환 색이 있으면 시간에 따라 다음 색으로 부드럽게 넘어간다. offset 1은 한 색만큼 앞선다.
        Color ColorAt(float offset)
        {
            var cycle = _style.Cycle;
            if (cycle == null || cycle.Length == 0)
            {
                return _style.Color;
            }

            var t = Time.time / Mathf.Max(0.05f, _style.CycleStep) + offset;
            var from = Mathf.FloorToInt(t);
            var blend = Mathf.SmoothStep(0f, 1f, t - from);
            var index = ((from % cycle.Length) + cycle.Length) % cycle.Length;
            return Color.Lerp(cycle[index], cycle[(index + 1) % cycle.Length], blend);
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
            var sprite = view != null && view.data != null && view.data.HasFrames
                ? view.data.Frames[0]
                : view != null && view.Renderer != null ? view.Renderer.sprite : null;
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

        static Sprite BangSprite()
        {
            if (_bang == null)
            {
                _bang = MakeSprite("RarityGlowBang", 32, 32, 0.5f, 0.5f, 32f, PaintBang);
            }

            return _bang;
        }

        static Sprite ShardSprite()
        {
            if (_shard == null)
            {
                _shard = MakeSprite("RarityGlowShard", 32, 32, 0.5f, 0.5f, 32f, PaintShard);
            }

            return _shard;
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

        static void PaintBang(Color32[] pixels, int width, int height)
        {
            var cx = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = (x - cx) / cx;
                    var ny = y / (float)(height - 1);
                    var bar = ny > 0.28f && ny < 0.78f && Mathf.Abs(nx) < 0.16f;
                    var dot = ny < 0.2f && nx * nx + ((ny - 0.1f) / 0.12f) * ((ny - 0.1f) / 0.12f) < 1f;
                    var alpha = bar || dot ? 1f : 0f;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
        }

        // 세로로 긴 마름모. 왼쪽 면을 밝게 칠해 프리즘 조각처럼 보이게 한다.
        static void PaintShard(Color32[] pixels, int width, int height)
        {
            var cx = (width - 1) * 0.5f;
            var cy = (height - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = (x - cx) / cx;
                    var ny = (y - cy) / cy;
                    var d = Mathf.Abs(nx) * 2.2f + Mathf.Abs(ny);
                    var edge = Mathf.Clamp01((1f - d) * 8f);
                    var facet = nx < 0f ? 1f : 0.6f;
                    var alpha = edge * facet;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
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
