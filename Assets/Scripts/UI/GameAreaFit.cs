using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class GameAreaFit : MonoBehaviour
    {
        const float Line = 2f;

        RectTransform _canvasRect;
        RectTransform _area;
        RectTransform _frame;
        RectTransform _top;
        RectTransform _bottom;
        RectTransform _left;
        RectTransform _right;
        Rect _lastPixel;
        Vector2 _lastCanvas;

        public RectTransform Area => _area;

        public static GameAreaFit Ensure(Canvas canvas)
        {
            var fit = canvas.GetComponent<GameAreaFit>();
            if (fit == null)
            {
                fit = canvas.gameObject.AddComponent<GameAreaFit>();
            }

            fit.Build();
            return fit;
        }

        public void Build()
        {
            _canvasRect = transform as RectTransform;
            if (_area == null)
            {
                var found = transform.Find("GameArea") as RectTransform;
                _area = found != null ? found : CreateRect("GameArea");
            }

            if (_frame == null)
            {
                var found = transform.Find("LetterboxFrame") as RectTransform;
                _frame = found != null ? found : CreateRect("LetterboxFrame");
                StretchFull(_frame);
                _top = EnsureBar(_frame, "Top");
                _bottom = EnsureBar(_frame, "Bottom");
                _left = EnsureBar(_frame, "Left");
                _right = EnsureBar(_frame, "Right");
            }

            _frame.SetAsFirstSibling();
            _area.SetSiblingIndex(1);
            Canvas.ForceUpdateCanvases();
            Apply();
            Adopt();
            LiftSmallText(_area);
        }

        public void Adopt()
        {
            if (_area == null)
            {
                return;
            }

            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == _area || child == _frame)
                {
                    continue;
                }

                child.SetParent(_area, false);
            }
        }

        void LateUpdate()
        {
            Apply();
        }

        void Apply()
        {
            if (_canvasRect == null || _area == null)
            {
                return;
            }

            var pixel = GamePixelRect();
            var canvasSize = _canvasRect.rect.size;
            if (pixel == _lastPixel && canvasSize == _lastCanvas)
            {
                return;
            }

            _lastPixel = pixel;
            _lastCanvas = canvasSize;
            if (!ScreenToLocal(new Vector2(pixel.xMin, pixel.yMin), out var min)
                || !ScreenToLocal(new Vector2(pixel.xMax, pixel.yMax), out var max))
            {
                return;
            }

            _area.anchorMin = new Vector2(0.5f, 0.5f);
            _area.anchorMax = new Vector2(0.5f, 0.5f);
            _area.pivot = new Vector2(0.5f, 0.5f);
            _area.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            _area.anchoredPosition = (min + max) * 0.5f;
            FitBars(canvasSize, _area.sizeDelta, _area.anchoredPosition);
        }

        void FitBars(Vector2 canvas, Vector2 area, Vector2 center)
        {
            var half = canvas * 0.5f;
            var top = Mathf.Max(0f, half.y - (center.y + area.y * 0.5f));
            var bottom = Mathf.Max(0f, center.y - area.y * 0.5f + half.y);
            var left = Mathf.Max(0f, center.x - area.x * 0.5f + half.x);
            var right = Mathf.Max(0f, half.x - (center.x + area.x * 0.5f));

            SetBar(_top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, top), true);
            SetBar(_bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom), true);
            SetBar(_left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(left, 0f), false);
            SetBar(_right, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(right, 0f), false);
        }

        static void SetBar(RectTransform bar, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size, bool horizontal)
        {
            if (bar == null)
            {
                return;
            }

            bar.anchorMin = min;
            bar.anchorMax = max;
            bar.pivot = pivot;
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = size;
            bar.gameObject.SetActive(size.x > 0.5f || size.y > 0.5f);
            var line = bar.Find("Line") as RectTransform;
            if (line == null)
            {
                return;
            }

            if (horizontal)
            {
                var inner = pivot.y > 0.5f;
                line.anchorMin = new Vector2(0f, inner ? 0f : 1f);
                line.anchorMax = new Vector2(1f, inner ? 0f : 1f);
                line.pivot = new Vector2(0.5f, inner ? 0f : 1f);
                line.sizeDelta = new Vector2(0f, Line);
            }
            else
            {
                var inner = pivot.x < 0.5f;
                line.anchorMin = new Vector2(inner ? 1f : 0f, 0f);
                line.anchorMax = new Vector2(inner ? 1f : 0f, 1f);
                line.pivot = new Vector2(inner ? 1f : 0f, 0.5f);
                line.sizeDelta = new Vector2(Line, 0f);
            }

            line.anchoredPosition = Vector2.zero;
        }

        bool ScreenToLocal(Vector2 screen, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local);
        }

        static Rect GamePixelRect()
        {
            if (FixedResolution.Current != null && FixedResolution.Current.GameCamera != null)
            {
                return FixedResolution.Current.GameCamera.pixelRect;
            }

            return new Rect(0f, 0f, Screen.width, Screen.height);
        }

        RectTransform CreateRect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            return (RectTransform)go.transform;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static RectTransform EnsureBar(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.color = Palette.Background;
            image.raycastTarget = false;
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(go.transform, false);
            lineGo.AddComponent<RectTransform>();
            var line = lineGo.AddComponent<Image>();
            var neon = Palette.Cyan;
            neon.a = 0.4f;
            line.color = neon;
            line.raycastTarget = false;
            return rect;
        }

        static void LiftSmallText(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var texts = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].fontSize < 16)
                {
                    texts[i].fontSize = 22;
                }
            }
        }
    }
}
