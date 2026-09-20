using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ResultsView : MonoBehaviour
    {
        GameObject _root;
        Text _summary;

        public void Build(Transform canvas)
        {
            _root = new GameObject("Results");
            _root.transform.SetParent(canvas, false);
            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = _root.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.06f, 0.84f);
            background.raycastTarget = false;

            _summary = UiFonts.CreateText(rect, "Summary", 24, TextAnchor.MiddleCenter);
            _summary.rectTransform.anchorMin = new Vector2(0.08f, 0.32f);
            _summary.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
            _summary.rectTransform.offsetMin = Vector2.zero;
            _summary.rectTransform.offsetMax = Vector2.zero;

            Hide();
        }

        public void Show(RunSession session)
        {
            var best = session.HighestGrade();
            var bestName = best != null ? best.DisplayName : "없음";
            _summary.text = $"정산\n스크랩 {session.TotalValue}\n최고 등급 {bestName}";
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }
    }
}
