using ScrapFishing.Core;
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
            Stretch(_scrap.rectTransform, new Vector2(0.5f, 0.90f), new Vector2(0.95f, 0.98f));

            _hint = UiFonts.CreateText(rect, "Hint", 16, TextAnchor.LowerCenter);
            Stretch(_hint.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.12f));
            _hint.color = new Color(0.75f, 0.9f, 1f, 0.9f);
        }

        public void Refresh(GameFlow flow, RunSession session, float gauge)
        {
            if (_depth == null)
            {
                return;
            }

            var playing = flow.Phase != GamePhase.Title && flow.Phase != GamePhase.Results;
            var meters = 8f + gauge * 42f;
            _time.text = playing ? $"TIME {session.Remaining:0}" : string.Empty;
            _depth.text = playing ? $"DEPTH {meters:0}m" : string.Empty;
            _scrap.text = playing ? $"SCRAP {session.TotalValue}" : string.Empty;
            _hint.text = HintFor(flow.Phase);
        }

        static string HintFor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Aiming:
                    return "탭으로 캐스팅";
                case GamePhase.Casting:
                    return "훅 하강 중";
                case GamePhase.Reeling:
                    return "스와이프로 건져 올려";
                case GamePhase.CastComplete:
                    return "탭해서 다시 캐스팅";
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
