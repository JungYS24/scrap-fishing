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
        float _lastCamY;
        float _parallax;
        Tween _fade;

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
            _parallax += (_lastCamY - camY) * 0.1f;
            _parallax = Mathf.Clamp(_parallax, -0.35f, 0.35f);
            _lastCamY = camY;

            var depth = DepthZone.FromWorldY(camY);
            var zone = DepthZone.Index(depth);
            if (_zone < 0)
            {
                Show(zone, true);
            }
            else if (zone != _zone)
            {
                var edge = zone > _zone ? _zone * 0.25f + 0.25f : _zone * 0.25f;
                if (Mathf.Abs(depth - edge) >= 0.04f)
                {
                    Show(zone, false);
                }
            }

            var y = Mathf.Min(camY, ceiling) + _parallax;
            FitAndPlace(_current, cam, y);
            FitAndPlace(_next, cam, y);
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
                _next.color = new Color(1f, 1f, 1f, 0f);
                _zone = zone;
                HideColorBands();
                return;
            }

            _next.sprite = sprite;
            _next.color = new Color(1f, 1f, 1f, 0f);
            if (_fade != null && _fade.IsActive())
            {
                _fade.Kill(false);
            }

            var from = _current;
            var to = _next;
            _fade = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Join(FadeSprite(to, 1f, 0.4f))
                .Join(FadeSprite(from, 0f, 0.4f))
                .OnComplete(() =>
                {
                    _current = to;
                    _next = from;
                    _next.color = new Color(1f, 1f, 1f, 0f);
                });
            _zone = zone;
            HideColorBands();
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

            _baked[index] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
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

            var worldH = cam.orthographicSize * 2f;
            var worldW = worldH * cam.aspect;
            var s = Mathf.Max(worldW / size.x, worldH / size.y);
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
            if (_shared != null)
            {
                renderer.sharedMaterial = _shared;
            }

            return renderer;
        }
    }
}
