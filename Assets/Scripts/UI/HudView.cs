using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class HudView : MonoBehaviour
    {
        [SerializeField] UiFxSettings settings;

        Text _time;
        Text _depth;
        Text _cy;
        GameObject _cyPanel;
        Text _dive;
        Text _hint;
        GameObject _oxygenBar;
        Image _oxygenFill;
        Text _toast;
        CanvasGroup _toastGroup;
        RectTransform _toastRect;
        Vector2 _toastHome;
        ChipFlyFx _chipFx;
        CountingText _counter;
        Sequence _toastSeq;
        CanvasGroup _hudGroup;
        CanvasGroup _chipGroup;
        CanvasGroup _oxygenGroup;
        Vector2 _chipHome;
        Vector3 _chipScale = Vector3.one;
        Sequence _hudIntro;
        RunSession _session;
        int _lastTime = int.MinValue;
        string _lastDepth = string.Empty;
        string _lastHint = string.Empty;
        int _lastCY;
        bool _playing;
        bool _oxygenShown;

        public void Build(Transform canvas, Text chipText, GameObject chipPanel)
        {
            settings = Resources.Load<UiFxSettings>("UiFxSettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<UiFxSettings>();
            }

            var root = new GameObject("Hud");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _cy = chipText;
            _cyPanel = chipPanel;

            if (chipText != null && chipText.fontSize < 16)
            {
                chipText.fontSize = 22;
            }

            _depth = UiFonts.CreateText(rect, "Depth", 18, TextAnchor.UpperLeft);
            Stretch(_depth.rectTransform, new Vector2(0.06f, 0.80f), new Vector2(0.5f, 0.88f));

            _time = UiFonts.CreateText(rect, "Time", 18, TextAnchor.UpperRight);
            Stretch(_time.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(0.94f, 0.96f));

            _dive = UiFonts.CreateText(rect, "DiveProgress", 18, TextAnchor.UpperRight);
            Stretch(_dive.rectTransform, new Vector2(0.5f, 0.83f), new Vector2(0.94f, 0.88f));

            _hint = UiFonts.CreateText(rect, "Hint", 16, TextAnchor.MiddleRight);
            Stretch(_hint.rectTransform, new Vector2(0.36f, 0.06f), new Vector2(0.88f, 0.16f));
            _hint.color = new Color(0.75f, 0.9f, 1f, 0.9f);

            BuildToast(rect);
            var banner = UiFonts.CreateText(rect, "MutantBanner", 28, TextAnchor.MiddleCenter);
            Stretch(banner.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.72f));
            banner.color = Palette.NeonGreen;
            banner.gameObject.SetActive(false);

            BindChip(rect, banner);
            BuildOxygenBar(rect);
            _hudGroup = root.AddComponent<CanvasGroup>();
            _hudGroup.alpha = 0f;
        }

        public void Bind(RunSession session)
        {
            if (_session != null)
            {
                _session.CaughtAt -= HandleCaught;
            }

            _session = session;
            if (_session != null)
            {
                _session.CaughtAt += HandleCaught;
            }
        }

        void OnDestroy()
        {
            if (_session != null)
            {
                _session.CaughtAt -= HandleCaught;
            }

            KillHudIntro();
        }

        public void NotifyRunReset()
        {
            _lastCY = 0;
            if (_chipFx != null)
            {
                _chipFx.ResetVisual();
            }
            else if (_counter != null)
            {
                _counter.Snap(0);
            }
        }

        public void Refresh(GameFlow flow, RunSession session, float gauge, float diveFill, bool forcedAscent)
        {
            if (_depth == null)
            {
                return;
            }

            var playing = flow.Phase != GamePhase.Title && flow.Phase != GamePhase.Results;
            var becamePlaying = !_playing && playing;
            if (_playing && !playing)
            {
                HideToast();
                HideHud();
                if (_chipFx != null)
                {
                    _chipFx.ResetVisual();
                }
            }

            _playing = playing;
            var diving = flow.Phase == GamePhase.Diving;
            SetTime(playing ? Mathf.CeilToInt(session.Remaining) : -1);
            SetDepth(playing && !diving ? $"DEPTH {DepthZone.Meters(gauge):0}m {DepthZone.Name(gauge)}" : string.Empty);
            SetHint(HintFor(flow, session, forcedAscent));
            RefreshChipPanel(session, playing);
            RefreshDive(session, playing && !diving);
            if (becamePlaying)
            {
                PlayHudIntro();
            }

            RefreshOxygen(diving);

            if (_oxygenFill != null)
            {
                var oxygen = 1f - diveFill;
                _oxygenFill.fillAmount = oxygen;
                _oxygenFill.color = Color.Lerp(Palette.Magenta, Palette.Cyan, oxygen);
            }
        }

        public void Notify(string message)
        {
            PlayToast(message, Palette.Cyan);
        }

        void BindChip(RectTransform hud, Text banner)
        {
            if (_cy != null)
            {
                _counter = _cy.GetComponent<CountingText>();
                if (_counter == null)
                {
                    _counter = _cy.gameObject.AddComponent<CountingText>();
                }

                _counter.Bind(settings, string.Empty, false);
                _counter.Snap(0);
            }

            var flyGo = new GameObject("ChipFlyLayer");
            flyGo.transform.SetParent(hud, false);
            var flyRect = flyGo.AddComponent<RectTransform>();
            Stretch(flyRect, Vector2.zero, Vector2.one);
            flyGo.transform.SetAsLastSibling();
            _chipFx = flyGo.AddComponent<ChipFlyFx>();

            RectTransform icon = null;
            RectTransform panel = null;
            if (_cyPanel != null)
            {
                panel = _cyPanel.GetComponent<RectTransform>();
                var found = _cyPanel.transform.Find("ChipIcon") as RectTransform;
                icon = found != null ? found : panel;
                _chipHome = panel.anchoredPosition;
                _chipScale = panel.localScale;
                _chipGroup = _cyPanel.GetComponent<CanvasGroup>();
                if (_chipGroup == null)
                {
                    _chipGroup = _cyPanel.AddComponent<CanvasGroup>();
                }
            }

            _chipFx.Bind(settings, flyRect, icon, panel, _counter, banner);
        }

        void PlayHudIntro()
        {
            KillHudIntro();
            var fade = settings != null ? settings.hudFadeDuration : 0.22f;
            var slide = settings != null ? settings.hudSlide : 24f;
            _hudIntro = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (_hudGroup != null)
            {
                _hudGroup.alpha = 0f;
                _hudIntro.Append(_hudGroup.DOFade(1f, fade));
            }

            if (_cyPanel != null && _chipGroup != null)
            {
                var panel = (RectTransform)_cyPanel.transform;
                _chipGroup.alpha = 0f;
                panel.anchoredPosition = _chipHome + Vector2.up * slide;
                panel.localScale = _chipScale;
                _hudIntro.Join(_chipGroup.DOFade(1f, fade));
                _hudIntro.Join(panel.DOAnchorPos(_chipHome, fade).SetEase(Ease.OutCubic));
            }
        }

        void HideHud()
        {
            KillHudIntro();
            if (_hudGroup != null)
            {
                _hudGroup.alpha = 0f;
            }

            if (_chipGroup != null)
            {
                _chipGroup.alpha = 0f;
            }

            if (_cyPanel != null)
            {
                var panel = (RectTransform)_cyPanel.transform;
                panel.DOKill(false);
                panel.anchoredPosition = _chipHome;
                panel.localScale = _chipScale;
            }
        }

        void KillHudIntro()
        {
            if (_hudIntro != null && _hudIntro.IsActive())
            {
                _hudIntro.Kill(false);
            }

            _hudIntro = null;
            if (_hudGroup != null)
            {
                _hudGroup.DOKill(false);
            }

            if (_chipGroup != null)
            {
                _chipGroup.DOKill(false);
            }
        }

        void BuildToast(RectTransform parent)
        {
            _toast = UiFonts.CreateText(parent, "Toast", 22, TextAnchor.MiddleCenter);
            _toastRect = _toast.rectTransform;
            _toastRect.anchorMin = new Vector2(0.5f, 0.5f);
            _toastRect.anchorMax = new Vector2(0.5f, 0.5f);
            _toastRect.pivot = new Vector2(0.5f, 0.5f);
            _toastRect.sizeDelta = new Vector2(400f, 72f);
            _toastHome = new Vector2(0f, 36f);
            _toastRect.anchoredPosition = _toastHome;
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toast.raycastTarget = false;
        }

        void HandleCaught(ScrapDefinition definition, Vector3 worldPos, int gain)
        {
            if (!_playing || definition == null)
            {
                return;
            }

            _lastCY = _session != null ? _session.CY : _lastCY + gain;
            var signed = gain > 0 ? "+" + gain : gain.ToString();
            PlayToast(signed + " " + definition.DisplayName, definition.PlaceholderColor);
            if (_chipFx != null)
            {
                var total = _session != null ? _session.CY : gain;
                _chipFx.Play(definition, worldPos, gain, total);
            }
        }

        void RefreshChipPanel(RunSession session, bool playing)
        {
            if (_cyPanel != null && _cyPanel.activeSelf != playing)
            {
                _cyPanel.SetActive(playing);
            }

            if (!playing)
            {
                _lastCY = session.CY;
                return;
            }

            if (session.CY < _lastCY)
            {
                PlayToast($"{session.CY - _lastCY} CY", Palette.Magenta);
                if (_chipFx != null)
                {
                    _chipFx.PlayLoss(_lastCY - session.CY, session.CY);
                }
                else if (_counter != null)
                {
                    _counter.PlayTo(session.CY);
                }
            }

            _lastCY = session.CY;
        }

        void PlayToast(string message, Color color)
        {
            if (_toast == null)
            {
                return;
            }

            if (_toastSeq != null && _toastSeq.IsActive())
            {
                _toastSeq.Kill(false);
            }

            _toastRect.DOKill(false);
            _toastGroup.DOKill(false);
            _toast.text = message;
            color.a = 1f;
            _toast.color = color;
            _toastGroup.alpha = 1f;
            _toastRect.anchoredPosition = _toastHome;
            _toastRect.localScale = Vector3.one * 0.6f;
            var pop = settings != null ? settings.toastPopDuration : 0.12f;
            var hold = settings != null ? settings.toastHoldDuration : 0.35f;
            var rise = settings != null ? settings.toastRiseDuration : 0.4f;
            var risePx = settings != null ? settings.toastRise : 52f;
            _toastSeq = DOTween.Sequence().SetUpdate(true).SetLink(_toast.gameObject);
            _toastSeq.Append(_toastRect.DOScale(1.1f, pop).SetEase(Ease.OutBack));
            _toastSeq.Append(_toastRect.DOScale(1f, pop * 0.7f));
            _toastSeq.AppendInterval(hold);
            _toastSeq.Append(_toastRect.DOAnchorPos(_toastHome + Vector2.up * risePx, rise).SetEase(Ease.OutQuad));
            _toastSeq.Join(_toastGroup.DOFade(0f, rise));
        }

        void HideToast()
        {
            if (_toastSeq != null && _toastSeq.IsActive())
            {
                _toastSeq.Kill(false);
            }

            if (_toastGroup != null)
            {
                _toastGroup.alpha = 0f;
            }
        }

        void SetTime(int remaining)
        {
            if (remaining == _lastTime)
            {
                return;
            }

            _lastTime = remaining;
            _time.text = remaining >= 0 ? $"TIME {remaining}" : string.Empty;
        }

        void SetDepth(string depth)
        {
            if (depth == _lastDepth)
            {
                return;
            }

            _lastDepth = depth;
            _depth.text = depth;
        }

        void SetHint(string hint)
        {
            if (hint == _lastHint)
            {
                return;
            }

            _lastHint = hint;
            _hint.text = hint;
        }

        void BuildOxygenBar(RectTransform parent)
        {
            _oxygenBar = new GameObject("OxygenBar");
            _oxygenBar.transform.SetParent(parent, false);
            var bar = _oxygenBar.AddComponent<RectTransform>();
            Stretch(bar, new Vector2(0.3f, 0.88f), new Vector2(0.7f, 0.93f));

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(_oxygenBar.transform, false);
            var trackRect = trackGo.AddComponent<RectTransform>();
            Stretch(trackRect, Vector2.zero, Vector2.one);
            var track = trackGo.AddComponent<Image>();
            track.sprite = PlaceholderFactory.Square(new Color(0.06f, 0.08f, 0.12f, 0.82f));
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(_oxygenBar.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            Stretch(fillRect, new Vector2(0.015f, 0.12f), new Vector2(0.985f, 0.88f));
            _oxygenFill = fillGo.AddComponent<Image>();
            _oxygenFill.sprite = PlaceholderFactory.Square(Palette.Cyan);
            _oxygenFill.type = Image.Type.Filled;
            _oxygenFill.fillMethod = Image.FillMethod.Horizontal;
            _oxygenFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _oxygenFill.fillAmount = 1f;
            _oxygenFill.raycastTarget = false;

            var label = UiFonts.CreateText(bar, "Label", 18, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.text = "산소";

            _oxygenGroup = _oxygenBar.AddComponent<CanvasGroup>();
            _oxygenBar.SetActive(false);
        }

        void RefreshOxygen(bool diving)
        {
            if (_oxygenBar == null)
            {
                return;
            }

            if (!diving)
            {
                _oxygenShown = false;
                _oxygenBar.SetActive(false);
                return;
            }

            if (_oxygenShown)
            {
                return;
            }

            _oxygenShown = true;
            _oxygenBar.SetActive(true);
            if (_oxygenGroup != null)
            {
                _oxygenGroup.DOKill(false);
                _oxygenGroup.alpha = 0f;
                _oxygenGroup.DOFade(1f, settings != null ? settings.hudFadeDuration : 0.22f)
                    .SetUpdate(true)
                    .SetLink(_oxygenBar);
            }
        }

        void RefreshDive(RunSession session, bool visible)
        {
            if (!visible)
            {
                _dive.text = string.Empty;
                return;
            }

            var filled = Mathf.Min(session.DiveProgress, RunSession.CastsPerDive);
            _dive.text = "잠수 " + new string('●', filled) + new string('○', RunSession.CastsPerDive - filled);
            _dive.color = session.CanDive ? Palette.Cyan : new Color(0.75f, 0.9f, 1f, 0.6f);
        }

        static string HintFor(GameFlow flow, RunSession session, bool forcedAscent)
        {
            switch (flow.Phase)
            {
                case GamePhase.Aiming:
                    return session.CanDive ? "깊게 탭할수록 고가!\n아래로 스와이프: 잠수" : "깊게 탭할수록 고가!";
                case GamePhase.Casting:
                    return "훅 하강 중";
                case GamePhase.Reeling:
                    return "스와이프로 낚아채기!";
                case GamePhase.CastComplete:
                    return session.CanDive ? "탭: 재캐스팅\n아래로 스와이프: 잠수" : "탭해서 다시 캐스팅";
                case GamePhase.Diving:
                    return forcedAscent ? "강제 부상" : "스틱으로 유영";
                default:
                    return string.Empty;
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
