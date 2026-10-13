using System;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.FX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ResultsView : MonoBehaviour
    {
        const float InputLock = 0.8f;

        GameObject _root;
        CanvasGroup _group;
        Image _dim;
        RectTransform _titleRect;
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
        int _targetCy;
        bool _counting;

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
            _dim = background;
            _group = _root.AddComponent<CanvasGroup>();

            var title = UiFonts.CreateText(rect, "Title", 28, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.80f));
            title.text = "정산";
            _titleRect = title.rectTransform;

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

            _restart = UiButton.Create(rect, "RestartButton", "재도전", new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.28f), () =>
            {
                if (_restartRect != null)
                {
                    Fx.Punch(_restartRect, 0.18f, 0.15f);
                }

                Fx.FadeOut(FXConfig.Current.fadeDuration, true);
                DOVirtual.DelayedCall(FXConfig.Current.fadeDuration, () => onRestart?.Invoke()).SetUpdate(true);
            });
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

            if (_counting && _root != null && _root.activeSelf && Pressed())
            {
                if (_cyCount != null)
                {
                    _cyCount.Snap(_targetCy);
                }

                _counting = false;
                if (_cyText != null)
                {
                    Fx.Punch(_cyText.transform, 0.16f, 0.16f);
                    Fx.FlashColor(_cyText, Color.white, 0.12f);
                }
            }
        }

        static bool Pressed()
        {
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        }

        public void Show(RunSession session)
        {
            var best = session.HighestGrade();
            var bestName = best != null ? best.DisplayName : "없음";
            _bestText.text = $"최고 기록 {session.BestCY} CY";
            _detail.text = $"건수 {session.Caught.Count}\n최고 등급 {bestName}\n달러 +{session.EarnedDollars} (보유 $ {Progression.Dollars})";
            _newBest.SetActive(session.IsNewBest);
            _targetCy = session.CY;
            _counting = true;
            _readyAt = Time.unscaledTime + InputLock;
            _restart.interactable = false;
            _root.SetActive(true);
            if (session.IsNewBest)
            {
                AudioManager.Ensure().PlayNewBest();
            }

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
            if (_dim != null)
            {
                var dim = _dim.color;
                dim.a = 0f;
                _dim.color = dim;
                _dim.DOFade(0.6f, fade).SetUpdate(true).SetLink(_root);
            }

            if (_cyCount != null)
            {
                _cyCount.Snap(0);
                _cyCount.PlayTo(session.CY);
                DOVirtual.DelayedCall(0.45f, () =>
                {
                    if (_counting && _cyText != null)
                    {
                        _counting = false;
                        Fx.Punch(_cyText.transform, 0.16f, 0.16f);
                        Fx.FlashColor(_cyText, Color.white, 0.12f);
                    }
                }).SetUpdate(true).SetLink(_root);
            }
            else
            {
                _cyText.text = $"획득 {session.CY} CY";
            }

            if (_restartRect != null)
            {
                _restartRect.localScale = Vector3.zero;
            }

            var stagger = FXConfig.Current.resultsStagger;
            Fx.Pop(_root.transform);
            if (_titleRect != null)
            {
                Fx.Pop(_titleRect);
            }

            _intro = DOTween.Sequence().SetUpdate(true).SetLink(_root);
            _intro.Append(_group.DOFade(1f, fade));
            if (_cyText != null)
            {
                _cyText.transform.localScale = Vector3.zero;
                _intro.Join(_cyText.transform.DOScale(1f, fade).SetDelay(stagger).SetEase(Ease.OutBack).SetUpdate(true));
            }

            if (_detail != null)
            {
                _detail.transform.localScale = Vector3.zero;
                _intro.Join(_detail.transform.DOScale(1f, fade).SetDelay(stagger * 2f).SetEase(Ease.OutBack).SetUpdate(true));
            }

            if (_bestText != null)
            {
                _bestText.transform.localScale = Vector3.zero;
                _intro.Join(_bestText.transform.DOScale(1f, fade).SetDelay(stagger * 3f).SetEase(Ease.OutBack).SetUpdate(true));
            }

            if (session.IsNewBest && _bestRect != null)
            {
                _bestRect.localScale = Vector3.zero;
                _intro.Join(_bestRect.DOScale(_bestScale, bestDur).SetEase(Ease.OutBack));
                _bestRect.DORotate(new Vector3(0f, 0f, 5f), 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(_newBest);
                var bestText = _newBest.GetComponent<Text>();
                if (bestText != null)
                {
                    bestText.DOColor(Color.white, 0.4f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_newBest);
                }

                Fx.Burst(_bestRect.position, null, 8, Palette.Gold);
            }

            _intro.AppendInterval(Mathf.Max(0f, InputLock - fade));
            if (_restartRect != null)
            {
                _intro.Append(_restartRect.DOScale(_restartScale, buttonDur).SetEase(Ease.OutBack));
                _intro.OnComplete(() =>
                {
                    if (_restartRect != null)
                    {
                        _restartRect.DOScale(_restartScale * 1.05f, 0.7f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine)
                            .SetUpdate(true)
                            .SetLink(_restart.gameObject);
                    }
                });
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
