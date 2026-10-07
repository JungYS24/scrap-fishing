using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class HudView : MonoBehaviour
    {
        Text _time;
        Text _depth;
        Text _cy;
        Text _hint;
        GameObject _oxygenBar;
        Image _oxygenFill;
        Text _toast;
        Color _toastColor;
        float _toastUntil;
        int _lastCatchCount;
        int _lastCY;

        public void Build(Transform canvas)
        {
            var root = new GameObject("Hud");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _cy = UiFonts.CreateText(rect, "CY", 18, TextAnchor.UpperLeft);
            Stretch(_cy.rectTransform, new Vector2(0.05f, 0.90f), new Vector2(0.5f, 0.98f));
            _cy.color = Palette.NeonGreen;

            _depth = UiFonts.CreateText(rect, "Depth", 18, TextAnchor.UpperLeft);
            Stretch(_depth.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.5f, 0.90f));

            _time = UiFonts.CreateText(rect, "Time", 18, TextAnchor.UpperRight);
            Stretch(_time.rectTransform, new Vector2(0.5f, 0.90f), new Vector2(0.95f, 0.98f));

            _hint = UiFonts.CreateText(rect, "Hint", 16, TextAnchor.MiddleRight);
            Stretch(_hint.rectTransform, new Vector2(0.38f, 0.04f), new Vector2(0.88f, 0.14f));
            _hint.color = new Color(0.75f, 0.9f, 1f, 0.9f);

            _toast = UiFonts.CreateText(rect, "Toast", 22, TextAnchor.MiddleCenter);
            Stretch(_toast.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.56f));
            _toast.color = Palette.NeonGreen;
            _toast.text = string.Empty;

            BuildOxygenBar(rect);
        }

        public void Refresh(GameFlow flow, RunSession session, float gauge, float diveFill, bool forcedAscent)
        {
            if (_depth == null)
            {
                return;
            }

            var playing = flow.Phase != GamePhase.Title && flow.Phase != GamePhase.Results;
            var diving = flow.Phase == GamePhase.Diving;
            var meters = DepthZone.Meters(gauge);
            _time.text = playing ? $"TIME {session.Remaining:0}" : string.Empty;
            _depth.text = diving ? string.Empty : playing ? $"DEPTH {meters:0}m {DepthZone.Name(gauge)}" : string.Empty;
            _cy.text = playing ? $"{session.CY} CY" : string.Empty;
            _hint.text = HintFor(flow, session, forcedAscent);
            RefreshToast(session, playing);
            if (_oxygenBar != null)
            {
                _oxygenBar.SetActive(diving);
            }

            if (_oxygenFill != null)
            {
                var oxygen = 1f - diveFill;
                _oxygenFill.fillAmount = oxygen;
                _oxygenFill.color = Color.Lerp(Palette.Magenta, Palette.Cyan, oxygen);
            }
        }

        void BuildOxygenBar(RectTransform parent)
        {
            _oxygenBar = new GameObject("OxygenBar");
            _oxygenBar.transform.SetParent(parent, false);
            var bar = _oxygenBar.AddComponent<RectTransform>();
            Stretch(bar, new Vector2(0.3f, 0.92f), new Vector2(0.7f, 0.965f));

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

            _oxygenBar.SetActive(false);
        }

        void RefreshToast(RunSession session, bool playing)
        {
            if (_toast == null)
            {
                return;
            }

            var count = session.Caught.Count;
            var cy = session.CY;
            if (!playing)
            {
                _lastCatchCount = count;
                _lastCY = cy;
                _toast.text = string.Empty;
                return;
            }

            if (count > _lastCatchCount)
            {
                var latest = session.Caught[count - 1];
                ShowToast($"+{latest.Value} {latest.DisplayName}", Palette.NeonGreen);
            }
            else if (cy < _lastCY)
            {
                ShowToast($"{cy - _lastCY} CY", Palette.Magenta);
            }

            _lastCatchCount = count;
            _lastCY = cy;
            if (string.IsNullOrEmpty(_toast.text) || Time.unscaledTime > _toastUntil)
            {
                _toast.text = string.Empty;
                Stretch(_toast.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.56f));
                return;
            }

            var t = Mathf.Clamp01(1f - (_toastUntil - Time.unscaledTime) / 0.95f);
            var rise = 0.08f * t;
            Stretch(_toast.rectTransform, new Vector2(0.15f, 0.42f + rise), new Vector2(0.85f, 0.56f + rise));
            var color = _toastColor;
            color.a = 1f - t * 0.35f;
            _toast.color = color;
        }

        void ShowToast(string message, Color color)
        {
            _toast.text = message;
            _toastColor = color;
            _toastUntil = Time.unscaledTime + 0.95f;
        }

        static string HintFor(GameFlow flow, RunSession session, bool forcedAscent)
        {
            switch (flow.Phase)
            {
                case GamePhase.Aiming:
                    return "깊게 탭할수록 고가!";
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
