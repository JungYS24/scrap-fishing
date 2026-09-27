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

        static string HintFor(GameFlow flow, RunSession session, bool forcedAscent)
        {
            switch (flow.Phase)
            {
                case GamePhase.Aiming:
                    return "탭으로 캐스팅";
                case GamePhase.Casting:
                    return "훅 하강 중";
                case GamePhase.Reeling:
                    return "스와이프로 건져 올려";
                case GamePhase.CastComplete:
                    return session.CanDive ? "탭해서 잠수" : "탭해서 다시 캐스팅";
                case GamePhase.Diving:
                    return forcedAscent ? "강제 부상" : "스틱으로 유영";
                case GamePhase.Results:
                    return "탭해서 타이틀";
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
