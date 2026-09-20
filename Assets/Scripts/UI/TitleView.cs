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
            Stretch(title.rectTransform, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.78f));
            title.text = "SCRAP FISHING";
            title.color = new Color(0.22f, 1f, 0.61f);

            var prompt = UiFonts.CreateText(rect, "Prompt", 22, TextAnchor.MiddleCenter);
            Stretch(prompt.rectTransform, new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.46f));
            prompt.text = "탭해서 출항";
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
