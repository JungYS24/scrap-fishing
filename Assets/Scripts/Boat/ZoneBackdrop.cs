using DG.Tweening;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Boat
{
    [DefaultExecutionOrder(200)]
    public class ZoneBackdrop : MonoBehaviour
    {
        [SerializeField] Sprite[] zones;

        SpriteRenderer _current;
        SpriteRenderer _next;
        ZoneBackdropArt _art;
        Sprite[] _baked;
        Material _shared;
        int _zone = -1;
        int _currentZone;
        int _nextZone = -1;
        float _followY;
        float _followVel;
        bool _followInit;
        bool _diveMode;
        Scroll _currentScroll;
        Scroll _nextScroll;
        Transform _diver;
        Tween _fade;
        Material _unlit;
        const float Overscan = 1.08f;
        const float CrossfadeSeconds = 0.4f;
        const float WidthFit = 1.02f;
        const float EdgeMargin = 0.64f;
        const float FollowSmooth = 0.15f;
        const float ZoneSpan = 0.25f;
        const float LegacyMaxAspect = 2.05f;
        const float DiveFollow = 0.15f;
        const float DiveTopY = 3.2f;
        const float DiveBottomY = -4f;

        struct Scroll
        {
            public float y;
            public float vel;
            public bool ready;
            public int zone;
        }

        public void SetDiveMode(bool dive)
        {
            _diveMode = dive;
            _followInit = false;
            _currentScroll.ready = false;
            _nextScroll.ready = false;
            if (dive)
            {
                ResolveDiver();
            }

            PinOrder();
        }

        void Awake()
        {
            _art = Resources.Load<ZoneBackdropArt>("Backgrounds/ZoneBackdropArt");
            if ((zones == null || zones.Length < 4) && _art != null)
            {
                zones = _art.Zones;
            }

            var city = GameObject.Find("Neocity");
            if (city != null && city.TryGetComponent<SpriteRenderer>(out var cityRenderer))
            {
                _shared = cityRenderer.sharedMaterial;
            }

            _current = MakeLayer("ZoneBackdropA", -15);
            _next = MakeLayer("ZoneBackdropB", -14);
            _next.color = new Color(1f, 1f, 1f, 0f);
            HideColorBands();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var camY = cam.transform.position.y;
            var half = cam.orthographicSize;
            var ceiling = SurfaceLayout.WaterlineY - half;
            var viewCenter = _diveMode ? camY : Mathf.Min(camY, ceiling);

            var depth = DepthZone.FromWorldY(camY);
            var zone = DepthZone.Index(depth);
            if (_zone < 0)
            {
                Show(zone, true);
            }
            else if (zone != _zone)
            {
                var edge = zone > _zone ? _zone * ZoneSpan + ZoneSpan : _zone * ZoneSpan;
                if (Mathf.Abs(depth - edge) >= 0.04f)
                {
                    Show(zone, false);
                }
            }

            if (IsShort(_current.sprite) || IsShort(_next.sprite))
            {
                FollowLegacy(viewCenter, cam);
                FitAndPlace(_current, cam, _followY);
                FitAndPlace(_next, cam, _followY);
                return;
            }

            Place(_current, cam, viewCenter, _currentZone, ref _currentScroll);
            Place(_next, cam, viewCenter, _nextZone, ref _nextScroll);
        }

        void OnDisable()
        {
            if (_fade != null && _fade.IsActive())
            {
                _fade.Kill(false);
            }
        }

        void Show(int zone, bool instant)
        {
            zone = Mathf.Clamp(zone, 0, 3);
            var sprite = Resolve(zone);
            if (sprite == null)
            {
                _current.sprite = null;
                _next.sprite = null;
                _zone = zone;
                HideColorBands();
                return;
            }

            if (instant || _current.sprite == null)
            {
                _current.sprite = sprite;
                _current.color = Color.white;
                _next.sprite = null;
                _next.color = new Color(1f, 1f, 1f, 0f);
                _zone = zone;
                _currentZone = zone;
                _currentScroll.ready = false;
                _nextZone = -1;
                HideColorBands();
                return;
            }

            if (_fade != null && _fade.IsActive())
            {
                _fade.Kill(false);
            }

            PinOrder();
            var back = _current.color;
            back.a = 1f;
            _current.color = back;
            _next.sprite = sprite;
            _next.color = new Color(1f, 1f, 1f, 0f);
            var incoming = _next;
            var outgoing = _current;
            _nextZone = zone;
            _nextScroll.ready = false;
            _fade = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(FadeSprite(incoming, 1f, CrossfadeSeconds))
                .OnComplete(() =>
                {
                    outgoing.sprite = incoming.sprite;
                    outgoing.color = Color.white;
                    incoming.sprite = null;
                    incoming.color = new Color(1f, 1f, 1f, 0f);
                    _currentZone = zone;
                    _currentScroll = _nextScroll;
                    _nextZone = -1;
                    _nextScroll.ready = false;
                    PinOrder();
                });
            _zone = zone;
            HideColorBands();
        }

        void PinOrder()
        {
            var order = _diveMode ? -21 : -15;
            if (_current != null)
            {
                _current.sortingOrder = order;
            }

            if (_next != null)
            {
                _next.sortingOrder = order + 1;
            }
        }

        Sprite Resolve(int index)
        {
            var assigned = SpriteAt(index);
            if (assigned != null)
            {
                return assigned;
            }

            var tex = _art != null ? _art.TextureAt(index) : null;
            if (tex == null)
            {
                return null;
            }

            if (_baked == null)
            {
                _baked = new Sprite[4];
            }

            if (_baked[index] != null)
            {
                return _baked[index];
            }

            var loaded = Resources.FindObjectsOfTypeAll<Sprite>();
            for (var i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null && loaded[i].texture == tex)
                {
                    _baked[index] = loaded[i];
                    return loaded[i];
                }
            }

            _baked[index] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), GameView.PixelsPerUnit);
            return _baked[index];
        }

        static void HideColorBands()
        {
            var surface = GameObject.Find("SurfaceProps");
            if (surface == null)
            {
                return;
            }

            var parent = surface.transform;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith("ZoneWater") && child.TryGetComponent<SpriteRenderer>(out var renderer))
                {
                    var color = renderer.color;
                    color.a = 0f;
                    renderer.color = color;
                    renderer.enabled = false;
                }
            }
        }

        Sprite SpriteAt(int index)
        {
            if (zones == null || index < 0 || index >= zones.Length)
            {
                return null;
            }

            return zones[index];
        }

        static Tween FadeSprite(SpriteRenderer renderer, float alpha, float dur)
        {
            return DOTween.To(() => renderer.color.a, a =>
            {
                var color = renderer.color;
                color.a = a;
                renderer.color = color;
            }, alpha, dur).SetTarget(renderer).SetUpdate(true).SetLink(renderer.gameObject);
        }

        void ResolveDiver()
        {
            if (_diver != null)
            {
                return;
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                _diver = player.transform;
            }
        }

        static bool IsShort(Sprite sprite)
        {
            if (sprite == null || sprite.rect.width < 1f)
            {
                return false;
            }

            return sprite.rect.height / sprite.rect.width < LegacyMaxAspect;
        }

        void FollowLegacy(float desired, Camera cam)
        {
            if (!_followInit)
            {
                _followY = desired;
                _followInit = true;
            }

            _followY = Mathf.SmoothDamp(_followY, desired, ref _followVel, 0.32f, Mathf.Infinity, Time.unscaledDeltaTime);
            var maxLag = cam.orthographicSize * (Overscan - 1f) * 0.85f;
            _followY = desired + Mathf.Clamp(_followY - desired, -maxLag, maxLag);
        }

        void Place(SpriteRenderer layer, Camera cam, float viewCenter, int zone, ref Scroll scroll)
        {
            var sprite = layer.sprite;
            if (sprite == null)
            {
                return;
            }

            var ppu = Mathf.Max(1f, sprite.pixelsPerUnit);
            var size = sprite.rect.size / ppu;
            if (size.x < 0.001f || size.y < 0.001f)
            {
                return;
            }

            ViewSize(cam, out var viewW, out var viewH);
            var scale = viewW / size.x * WidthFit;
            var minHeight = viewH + EdgeMargin * 2f;
            if (size.y * scale < minHeight)
            {
                scale = minHeight / size.y;
            }

            var height = size.y * scale;
            var top = viewCenter + viewH * 0.5f;
            var bottom = viewCenter - viewH * 0.5f;
            var y0 = top + EdgeMargin - height * 0.5f;
            var y1 = bottom - EdgeMargin + height * 0.5f;
            var low = Mathf.Min(y0, y1);
            var high = Mathf.Max(y0, y1);
            float target;
            if (_diveMode)
            {
                ResolveDiver();
                var travel = Mathf.Max(0.01f, DiveTopY - DiveBottomY);
                var diverY = _diver != null ? _diver.position.y : DiveTopY;
                var down = Mathf.Clamp01((DiveTopY - diverY) / travel);
                target = Mathf.Clamp(y0 + down * travel * DiveFollow, low, high);
            }
            else
            {
                var depth = DepthZone.FromWorldY(cam.transform.position.y);
                var t = Mathf.Clamp01((depth - zone * ZoneSpan) / ZoneSpan);
                target = Mathf.Lerp(y0, y1, t);
            }

            if (!scroll.ready || scroll.zone != zone)
            {
                scroll.y = target;
                scroll.vel = 0f;
                scroll.ready = true;
                scroll.zone = zone;
            }
            else
            {
                scroll.y = Mathf.SmoothDamp(scroll.y, target, ref scroll.vel, FollowSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            if (scroll.y < low || scroll.y > high)
            {
                scroll.y = Mathf.Clamp(scroll.y, low, high);
                scroll.vel = 0f;
            }

            var pivot = sprite.pivot;
            var center = new Vector3((sprite.rect.width * 0.5f - pivot.x) / ppu, (sprite.rect.height * 0.5f - pivot.y) / ppu, 0f);
            layer.transform.localScale = new Vector3(scale, scale, 1f);
            layer.transform.position = new Vector3(cam.transform.position.x, scroll.y, 0f) - center * scale;
        }

        static void ViewSize(Camera cam, out float viewW, out float viewH)
        {
            viewH = Mathf.Max(cam.orthographicSize * 2f, GameView.OrthoSize * 2f);
            viewW = viewH * GameView.Aspect;
            if (cam.pixelWidth > 8 && cam.pixelHeight > 8)
            {
                var pixelAspect = cam.pixelWidth / (float)cam.pixelHeight;
                if (pixelAspect < GameView.Aspect)
                {
                    viewH = Mathf.Max(viewH, viewW / Mathf.Max(0.01f, pixelAspect));
                }
                else if (pixelAspect <= GameView.Aspect * Overscan)
                {
                    viewW = Mathf.Max(viewW, viewH * pixelAspect);
                }
            }
        }

        static void FitAndPlace(SpriteRenderer layer, Camera cam, float y)
        {
            var sprite = layer.sprite;
            if (sprite == null)
            {
                return;
            }

            var size = sprite.bounds.size;
            if (size.x < 0.001f || size.y < 0.001f)
            {
                return;
            }

            ViewSize(cam, out var viewW, out var viewH);
            var s = Mathf.Max(viewW / size.x, viewH / size.y) * Overscan;
            layer.transform.localScale = new Vector3(s, s, 1f);
            var offset = sprite.bounds.center * s;
            layer.transform.position = new Vector3(cam.transform.position.x, y, 0f) - offset;
        }

        SpriteRenderer MakeLayer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            renderer.color = Color.white;
            var material = UnlitMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            else if (_shared != null)
            {
                renderer.sharedMaterial = _shared;
            }

            return renderer;
        }

        Material UnlitMaterial()
        {
            if (_unlit != null)
            {
                return _unlit;
            }

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _unlit = new Material(shader);
            return _unlit;
        }
    }
}
