using System;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public static class UiButton
    {
        public static Button Create(RectTransform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = Palette.Panel;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Palette.NeonGreen;
            outline.effectDistance = new Vector2(2f, -2f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = UiFonts.CreateText(rect, "Label", 28, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            text.color = Palette.NeonGreen;
            return button;
        }
    }
}
