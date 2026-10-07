using System;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ResultsView : MonoBehaviour
    {
        const float InputLock = 0.8f;

        GameObject _root;
        Text _cyText;
        Text _bestText;
        Text _detail;
        GameObject _newBest;
        Button _restart;
        float _readyAt;

        public void Build(Transform canvas, Action onRestart)
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

            var title = UiFonts.CreateText(rect, "Title", 28, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.80f));
            title.text = "정산";

            var newBest = UiFonts.CreateText(rect, "NewBestText", 28, TextAnchor.MiddleCenter);
            Stretch(newBest.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.70f));
            newBest.text = "NEW BEST!";
            newBest.color = Palette.Magenta;
            _newBest = newBest.gameObject;
            _newBest.SetActive(false);

            _cyText = UiFonts.CreateText(rect, "CyText", 28, TextAnchor.MiddleCenter);
            Stretch(_cyText.rectTransform, new Vector2(0.08f, 0.53f), new Vector2(0.92f, 0.62f));
            _cyText.color = Palette.NeonGreen;

            _bestText = UiFonts.CreateText(rect, "BestText", 22, TextAnchor.MiddleCenter);
            Stretch(_bestText.rectTransform, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.53f));

            _detail = UiFonts.CreateText(rect, "Detail", 18, TextAnchor.MiddleCenter);
            Stretch(_detail.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.46f));
            _detail.color = Palette.Cyan;

            _restart = UiButton.Create(rect, "RestartButton", "재도전", new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.28f), onRestart);

            Hide();
        }

        void Update()
        {
            if (_restart != null && _root.activeSelf)
            {
                _restart.interactable = Time.unscaledTime >= _readyAt;
            }
        }

        public void Show(RunSession session)
        {
            var best = session.HighestGrade();
            var bestName = best != null ? best.DisplayName : "없음";
            _cyText.text = $"획득 {session.CY} CY";
            _bestText.text = $"최고 기록 {session.BestCY} CY";
            _detail.text = $"건수 {session.Caught.Count}\n최고 등급 {bestName}";
            _newBest.SetActive(session.IsNewBest);
            _readyAt = Time.unscaledTime + InputLock;
            _restart.interactable = false;
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
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
