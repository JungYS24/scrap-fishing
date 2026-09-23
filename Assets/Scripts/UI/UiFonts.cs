using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public static class UiFonts
    {
        const string HudFont = "Fonts/Galmuri11";
        const string TitleFont = "Fonts/Galmuri14";

        public static Font Resolve(int size)
        {
            var path = size >= 24 ? TitleFont : HudFont;
            var font = Resources.Load<Font>(path);
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        public static Text CreateText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resolve(size);
            text.fontSize = SnapSize(size);
            text.fontStyle = FontStyle.Normal;
            text.alignment = anchor;
            text.alignByGeometry = true;
            text.color = Palette.Text;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static int SnapSize(int size)
        {
            if (size >= 24)
            {
                return 14 * Mathf.Max(1, Mathf.RoundToInt(size / 14f));
            }

            return 11 * Mathf.Max(2, Mathf.RoundToInt(size / 11f));
        }
    }
}
