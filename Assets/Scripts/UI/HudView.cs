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
        Text _scrap;
        Text _hint;
        GameObject _diveBar;
        Image _diveFill;
        Text _toast;
        float _toastUntil;
        int _lastCatchCount;

        public void Build(Transform canvas)
        {
            var root = new GameObject("Hud");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _time = UiFonts.CreateText(rect, "Time", 18, TextAnchor.UpperLeft);
            Stretch(_time.rectTransform, new Vector2(0.05f, 0.90f), new Vector2(0.5f, 0.98f));

            _depth = UiFonts.CreateText(rect, "Depth", 18, TextAnchor.UpperLeft);
            Stretch(_depth.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.5f, 0.90f));

            _scrap = UiFonts.CreateText(rect, "Scrap", 18, TextAnchor.UpperRight);
            Stretch(_scrap.rectTransform, new Vector2(0.4f, 0.90f), new Vector2(0.88f, 0.98f));

            _hint = UiFonts.CreateText(rect, "Hint", 16, TextAnchor.MiddleRight);
            Stretch(_hint.rectTransform, new Vector2(0.38f, 0.04f), new Vector2(0.88f, 0.14f));
            _hint.color = new Color(0.75f, 0.9f, 1f, 0.9f);

            _toast = UiFonts.CreateText(rect, "Toast", 22, TextAnchor.MiddleCenter);
            Stretch(_toast.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.56f));
            _toast.color = Palette.NeonGreen;
            _toast.text = string.Empty;

            BuildDiveBar(rect);
        }

        public void Refresh(GameFlow flow, RunSession session, float gauge, float diveFill, bool forcedAscent)
        {
            if (_depth == null)
            {
                return;
            }

            var playing = flow.Phase != GamePhase.Title && flow.Phase != GamePhase.Results;
            var diving = flow.Phase == GamePhase.Diving;
            var meters = 8f + gauge * 42f;
            _time.text = playing ? $"TIME {session.Remaining:0}" : string.Empty;
            _depth.text = diving ? string.Empty : playing ? $"DEPTH {meters:0}m" : string.Empty;
            _scrap.text = playing ? $"SCRAP {session.TotalValue}" : string.Empty;
            _hint.text = HintFor(flow, session, forcedAscent);
            RefreshToast(session, playing);
            if (_diveBar != null)
            {
                _diveBar.SetActive(diving);
            }

            if (_diveFill != null)
            {
                _diveFill.fillAmount = diveFill;
                _diveFill.color = Color.Lerp(Palette.Cyan, Palette.Magenta, diveFill);
            }
        }

        void BuildDiveBar(RectTransform parent)
        {
            _diveBar = new GameObject("DiveBar");
            _diveBar.transform.SetParent(parent, false);
            var bar = _diveBar.AddComponent<RectTransform>();
            Stretch(bar, new Vector2(0.905f, 0.16f), new Vector2(0.975f, 0.78f));

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(_diveBar.transform, false);
            var trackRect = trackGo.AddComponent<RectTransform>();
            Stretch(trackRect, Vector2.zero, Vector2.one);
            var track = trackGo.AddComponent<Image>();
            track.sprite = PlaceholderFactory.Square(new Color(0.06f, 0.08f, 0.12f, 0.82f));
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(_diveBar.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            Stretch(fillRect, new Vector2(0.18f, 0.03f), new Vector2(0.82f, 0.97f));
            _diveFill = fillGo.AddComponent<Image>();
            _diveFill.sprite = PlaceholderFactory.Square(Palette.Cyan);
            _diveFill.type = Image.Type.Filled;
            _diveFill.fillMethod = Image.FillMethod.Vertical;
            _diveFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _diveFill.fillAmount = 0f;
            _diveFill.raycastTarget = false;

            var label = UiFonts.CreateText(bar, "Label", 18, TextAnchor.LowerCenter);
            Stretch(label.rectTransform, new Vector2(-1.2f, 1.02f), new Vector2(2.2f, 1.14f));
            label.text = "잠수";
            label.color = Palette.Cyan;

            _diveBar.SetActive(false);
        }

        void RefreshToast(RunSession session, bool playing)
        {
            if (_toast == null)
            {
                return;
            }

            var count = session.Caught.Count;
            if (!playing)
            {
                _lastCatchCount = count;
                _toast.text = string.Empty;
                return;
            }

            if (count > _lastCatchCount)
            {
                var latest = session.Caught[count - 1];
                _toast.text = $"+{latest.Value} {latest.DisplayName}";
                _toastUntil = Time.unscaledTime + 0.95f;
            }

            _lastCatchCount = count;
            if (string.IsNullOrEmpty(_toast.text) || Time.unscaledTime > _toastUntil)
            {
                _toast.text = string.Empty;
                Stretch(_toast.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.56f));
                return;
            }

            var t = Mathf.Clamp01(1f - (_toastUntil - Time.unscaledTime) / 0.95f);
            var rise = 0.08f * t;
            Stretch(_toast.rectTransform, new Vector2(0.15f, 0.42f + rise), new Vector2(0.85f, 0.56f + rise));
            var color = Palette.NeonGreen;
            color.a = 1f - t * 0.35f;
            _toast.color = color;
        }

        static string HintFor(GameFlow flow, RunSession session, bool forcedAscent)
        {
            switch (flow.Phase)
            {
                case GamePhase.Aiming:
                    return "타이밍 맞춰 탭!";
                case GamePhase.Casting:
                    return "훅 하강 중";
                case GamePhase.Reeling:
                    return "스와이프로 낚아채기!";
                case GamePhase.CastComplete:
                    return session.CanDive ? "탭: 재캐스팅\n아래로 스와이프: 잠수" : "탭해서 다시 캐스팅";
                case GamePhase.Diving:
                    return forcedAscent ? "강제 부상" : "스틱으로 유영";
                case GamePhase.Results:
                    return "탭해서 재도전";
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
