using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 글자를 한 줄로 두고, 칸 폭을 넘으면 글자 크기를 줄인다(최소 min). 칸 크기가 바뀌면 다시 맞춘다.
    // 자동 축소(best fit)는 칸 높이가 두 줄을 허용하면 두 줄로 쪼개 버려서, 줄 수를 고정할 때 쓴다.
    [RequireComponent(typeof(Text))]
    public class TextOneLine : UIBehaviour
    {
        Text _text;
        int _max = 14;
        int _min = 10;

        public static TextOneLine Fit(Text text, int max, int min)
        {
            if (text == null)
            {
                return null;
            }

            var fit = text.GetComponent<TextOneLine>();
            if (fit == null)
            {
                fit = text.gameObject.AddComponent<TextOneLine>();
            }

            fit._max = max;
            fit._min = Mathf.Min(min, max);
            fit.Refresh();
            return fit;
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

            if (_text == null)
            {
                return;
            }

            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.resizeTextForBestFit = false;
            var rect = _text.rectTransform.rect;
            _text.fontSize = _max;
            if (rect.width <= 1f)
            {
                return;
            }

            var generator = new TextGenerator();
            for (var size = _max; size >= _min; size--)
            {
                _text.fontSize = size;
                var settings = _text.GetGenerationSettings(new Vector2(float.MaxValue, rect.height));
                if (generator.GetPreferredWidth(_text.text, settings) / _text.pixelsPerUnit <= rect.width)
                {
                    return;
                }
            }
        }
    }
}
