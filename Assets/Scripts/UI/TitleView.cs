using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class TitleView : MonoBehaviour
    {
        GameObject _root;

        public void Build(Transform canvas)
        {
            _root = new GameObject("Title");
            _root.transform.SetParent(canvas, false);
            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = _root.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);
            background.raycastTarget = false;

            var title = UiFonts.CreateText(rect, "TitleLabel", 42, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.8f));
            title.text = "SCRAP FISHING";
            title.color = Palette.NeonGreen;

            var prompt = UiFonts.CreateText(rect, "Prompt", 22, TextAnchor.MiddleCenter);
            Stretch(prompt.rectTransform, new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.56f));
            prompt.text = "탭해서 출항";

            var howTo = UiFonts.CreateText(rect, "HowTo", 16, TextAnchor.MiddleCenter);
            Stretch(howTo.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.42f));
            howTo.text = "깊게 탭할수록 고가\n스와이프 포획\n아래로 스와이프 잠수\n스틱 유영";
            howTo.color = Palette.Cyan;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
