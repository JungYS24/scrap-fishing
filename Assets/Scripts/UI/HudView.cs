using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class HudView : MonoBehaviour
    {
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

            _depth = UiFonts.CreateText(rect, "Depth", 18, TextAnchor.UpperLeft);
            Stretch(_depth.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.6f, 0.97f));

            _scrap = UiFonts.CreateText(rect, "Scrap", 18, TextAnchor.UpperRight);
            Stretch(_scrap.rectTransform, new Vector2(0.4f, 0.88f), new Vector2(0.95f, 0.97f));

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

            var meters = 8f + gauge * 42f;
            _depth.text = flow.Phase == GamePhase.Title ? string.Empty : $"DEPTH {meters:0}m";
            _scrap.text = $"SCRAP {session.TotalValue}";
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
