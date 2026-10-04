using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ResultsView : MonoBehaviour
    {
        const string BestKey = "BestScrap";
        const float InputLock = 0.8f;

        GameObject _root;
        Text _summary;
        Text _prompt;
        float _readyAt;

        public bool CanAcceptInput => _root != null && _root.activeSelf && Time.unscaledTime >= _readyAt;

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
            _summary.rectTransform.anchorMax = new Vector2(0.92f, 0.74f);
            _summary.rectTransform.offsetMin = Vector2.zero;
            _summary.rectTransform.offsetMax = Vector2.zero;

            _prompt = UiFonts.CreateText(rect, "Prompt", 22, TextAnchor.MiddleCenter);
            _prompt.rectTransform.anchorMin = new Vector2(0.1f, 0.14f);
            _prompt.rectTransform.anchorMax = new Vector2(0.9f, 0.28f);
            _prompt.rectTransform.offsetMin = Vector2.zero;
            _prompt.rectTransform.offsetMax = Vector2.zero;
            _prompt.color = new Color(0.75f, 0.9f, 1f, 0.9f);

            Hide();
        }

        public void Show(RunSession session)
        {
            var best = session.HighestGrade();
            var bestName = best != null ? best.DisplayName : "없음";
            var record = PlayerPrefs.GetInt(BestKey, 0);
            var isNew = session.TotalValue > record;
            if (isNew)
            {
                PlayerPrefs.SetInt(BestKey, session.TotalValue);
                PlayerPrefs.Save();
                record = session.TotalValue;
            }

            var recordLine = isNew ? "\nNEW RECORD" : $"\n최고 {record}";
            _summary.text = $"정산\n스크랩 {session.TotalValue}\n건수 {session.Caught.Count}\n최고 등급 {bestName}{recordLine}";
            _prompt.text = "탭해서 재도전";
            _readyAt = Time.unscaledTime + InputLock;
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
