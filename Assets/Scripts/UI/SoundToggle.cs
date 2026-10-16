using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public static class SoundToggle
    {
        public const string OnGlyph = "♪";
        public const string OffGlyph = "×";

        public static GameObject Create(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("SoundToggle");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.transform.SetAsLastSibling();

            var image = go.AddComponent<Image>();
            image.color = Palette.Panel;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Palette.NeonGreen;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var label = UiFonts.CreateText(rect, "Label", 22, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = Palette.NeonGreen;
            Refresh(label);

            button.onClick.AddListener(() =>
            {
                AudioManager.Ensure().ToggleSound();
                Refresh(label);
            });

            return go;
        }

        static void Refresh(Text label)
        {
            if (label == null)
            {
                return;
            }

            label.text = AudioManager.Ensure().SoundOn ? OnGlyph : OffGlyph;
        }
    }
}
