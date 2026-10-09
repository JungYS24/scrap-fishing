using System;
using DG.Tweening;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ResultsView : MonoBehaviour
    {
        const float InputLock = 0.8f;

        GameObject _root;
        CanvasGroup _group;
        Text _cyText;
        CountingText _cyCount;
        Text _bestText;
        Text _detail;
        GameObject _newBest;
        RectTransform _bestRect;
        Vector3 _bestScale = Vector3.one;
        Button _restart;
        RectTransform _restartRect;
        Vector3 _restartScale = Vector3.one;
        UiFxSettings _settings;
        Sequence _intro;
        float _readyAt;

        public void Build(Transform canvas, Action onRestart)
        {
            _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            _root = new GameObject("Results");
            _root.transform.SetParent(canvas, false);
            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = _root.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.06f, 0.84f);
            _group = _root.AddComponent<CanvasGroup>();

            var title = UiFonts.CreateText(rect, "Title", 28, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.80f));
            title.text = "정산";

            var newBest = UiFonts.CreateText(rect, "NewBestText", 28, TextAnchor.MiddleCenter);
            Stretch(newBest.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.70f));
            newBest.text = "NEW BEST!";
            newBest.color = Palette.Magenta;
            _newBest = newBest.gameObject;
            _bestRect = newBest.rectTransform;
            _bestScale = _bestRect.localScale;
            _newBest.SetActive(false);

            _cyText = UiFonts.CreateText(rect, "CyText", 28, TextAnchor.MiddleCenter);
            Stretch(_cyText.rectTransform, new Vector2(0.08f, 0.53f), new Vector2(0.92f, 0.62f));
            _cyText.color = Palette.NeonGreen;
            _cyCount = _cyText.gameObject.AddComponent<CountingText>();
            _cyCount.Bind(_settings, "획득 ", false, " CY", true);

            _bestText = UiFonts.CreateText(rect, "BestText", 22, TextAnchor.MiddleCenter);
            Stretch(_bestText.rectTransform, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.53f));

            _detail = UiFonts.CreateText(rect, "Detail", 18, TextAnchor.MiddleCenter);
            Stretch(_detail.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.46f));
            _detail.color = Palette.Cyan;

            _restart = UiButton.Create(rect, "RestartButton", "재도전", new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.28f), onRestart);
            _restartRect = _restart.transform as RectTransform;
            _restartScale = _restartRect.localScale;

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
            _bestText.text = $"최고 기록 {session.BestCY} CY";
            _detail.text = $"건수 {session.Caught.Count}\n최고 등급 {bestName}\n달러 +{session.EarnedDollars} (보유 $ {Progression.Dollars})";
            _newBest.SetActive(session.IsNewBest);
            _readyAt = Time.unscaledTime + InputLock;
            _restart.interactable = false;
            _root.SetActive(true);
            PlayIntro(session);
        }

        public void Hide()
        {
            KillIntro();
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        void PlayIntro(RunSession session)
        {
            KillIntro();
            var fade = _settings != null ? _settings.resultsFadeDuration : 0.25f;
            var bestDur = _settings != null ? _settings.resultsBestDuration : 0.35f;
            var buttonDur = _settings != null ? _settings.resultsButtonDuration : 0.22f;

            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            if (_cyCount != null)
            {
                _cyCount.Snap(0);
                _cyCount.PlayTo(session.CY);
            }
            else
            {
                _cyText.text = $"획득 {session.CY} CY";
            }

            if (_restartRect != null)
            {
                _restartRect.localScale = Vector3.zero;
            }

            _intro = DOTween.Sequence().SetUpdate(true).SetLink(_root);
            _intro.Append(_group.DOFade(1f, fade));
            if (session.IsNewBest && _bestRect != null)
            {
                _bestRect.localScale = Vector3.zero;
                _intro.Join(_bestRect.DOScale(_bestScale, bestDur).SetEase(Ease.OutBack));
            }

            _intro.AppendInterval(Mathf.Max(0f, InputLock - fade));
            if (_restartRect != null)
            {
                _intro.Append(_restartRect.DOScale(_restartScale, buttonDur).SetEase(Ease.OutBack));
            }
        }

        void KillIntro()
        {
            if (_intro != null && _intro.IsActive())
            {
                _intro.Kill(false);
            }

            _intro = null;
            if (_root != null)
            {
                _root.transform.DOKill(false);
            }

            if (_bestRect != null)
            {
                _bestRect.DOKill(false);
                _bestRect.localScale = _bestScale;
            }

            if (_restartRect != null)
            {
                _restartRect.DOKill(false);
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
