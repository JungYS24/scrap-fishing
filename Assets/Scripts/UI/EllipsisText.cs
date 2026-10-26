using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 글자를 최대 줄 수 안에 넣고, 넘치면 뒤를 잘라 말줄임(…)을 붙인다. 칸 크기가 바뀌면 다시 자른다.
    [RequireComponent(typeof(Text))]
    public class EllipsisText : UIBehaviour
    {
        Text _text;
        string _full = string.Empty;
        int _maxLines = 2;

        public string Full => _full;
        public bool Truncated { get; private set; }

        public void Set(string full, int maxLines)
        {
            _full = full ?? string.Empty;
            _maxLines = Mathf.Max(1, maxLines);
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_text == null)
            {
                _text = GetComponent<Text>();
            }

            var width = ((RectTransform)transform).rect.width;
            if (_text == null || width <= 1f)
            {
                if (_text != null)
                {
                    _text.text = _full;
                }

                return;
            }

            var settings = _text.GetGenerationSettings(new Vector2(width, 0f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            settings.generateOutOfBounds = true;
            var generator = new TextGenerator();
            if (Lines(generator, _full, settings) <= _maxLines)
            {
                Truncated = false;
                _text.text = _full;
                return;
            }

            // 글꼴에 … 글리프가 없으면 마침표 세 개로.
            var mark = _text.font != null && _text.font.HasCharacter('…') ? "…" : "...";
            var lo = 0;
            var hi = _full.Length;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (Lines(generator, _full.Substring(0, mid).TrimEnd() + mark, settings) <= _maxLines)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            Truncated = true;
            _text.text = _full.Substring(0, lo).TrimEnd() + mark;
        }

        static int Lines(TextGenerator generator, string text, TextGenerationSettings settings)
        {
            generator.Populate(text, settings);
            return generator.lineCount;
        }
    }
}
