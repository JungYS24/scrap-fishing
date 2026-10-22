using System;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 암시장 화면 공용 생성 도구. 이미지 에셋이 아직 없으므로 색 사각형 + 글자로 대신한다.
    public static class GachaUi
    {
        public static readonly Color NormalColor = new Color(0.6f, 0.64f, 0.7f);
        public static readonly Color RareColor = new Color(0.247f, 0.663f, 1f);
        public static readonly Color EpicColor = new Color(0.706f, 0.361f, 1f);
        public static readonly Color LegendColor = new Color(1f, 0.604f, 0.18f);
        public static readonly Color SynchroColor = Color.white;
        // Outline은 메시를 테두리 색으로 한 번 더 그리므로, 테두리가 있는 패널은 불투명해야 색이 번지지 않는다.
        public static readonly Color Backdrop = new Color(0.035f, 0.055f, 0.11f, 1f);
        public static readonly Color Card = new Color(0.06f, 0.09f, 0.17f, 1f);
        public static readonly Color Inset = new Color(0.03f, 0.04f, 0.08f, 1f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color Muted = new Color(0.62f, 0.68f, 0.78f);
        public static readonly Color Disabled = new Color(0.35f, 0.38f, 0.44f);

        static Sprite _dollarSprite;
        static bool _dollarSearched;

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return RareColor;
                case ItemRarity.Epic: return EpicColor;
                case ItemRarity.Legend: return LegendColor;
                case ItemRarity.Synchro: return SynchroColor;
                default: return NormalColor;
            }
        }

        public static string RarityLabel(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return "RARE";
                case ItemRarity.Epic: return "EPIC";
                case ItemRarity.Legend: return "LEGEND";
                case ItemRarity.Synchro: return "SYNCHRO";
                default: return "NORMAL";
            }
        }

        public static string SlotLabel(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Boat: return "보트";
                case GearSlot.Drone: return "드론";
                case GearSlot.Suit: return "수트";
                default: return "루어";
            }
        }

        public static Color SlotTint(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Boat: return Palette.Cyan;
                case GearSlot.Drone: return Palette.Magenta;
                case GearSlot.Suit: return Palette.Gold;
                default: return Palette.NeonGreen;
            }
        }

        public static string Money(int amount)
        {
            return "$" + Mathf.Max(0, amount).ToString("N0");
        }

        public static string Percent(float rate)
        {
            return (rate * 100f).ToString("0.0") + "%";
        }

        public static string Hex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect, min, max);
            return rect;
        }

        public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Image Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color, Color? outline = null, bool raycast = false)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (outline.HasValue)
            {
                var line = rect.gameObject.AddComponent<Outline>();
                line.effectColor = outline.Value;
                line.effectDistance = new Vector2(2f, -2f);
            }

            return image;
        }

        public static Text Label(Transform parent, string name, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color color, string value = "")
        {
            var text = UiFonts.CreateText(parent, name, size, anchor);
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.supportRichText = true;
            Stretch(text.rectTransform, min, max);
            return text;
        }

        public static Button MakeButton(RectTransform parent, string name, string label, Vector2 min, Vector2 max, Color accent, int size, Action onClick)
        {
            var button = UiButton.Create(parent, name, label, min, max, onClick);
            if (button.targetGraphic is Image fill)
            {
                fill.color = Inset;
            }

            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = accent;
            }

            var text = LabelOf(button);
            if (text != null)
            {
                text.fontSize = size;
                text.color = accent;
                text.supportRichText = true;
            }

            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.6f);
            button.colors = colors;
            return button;
        }

        public static Text LabelOf(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }

        // 회색 처리. accent는 다시 켤 때 돌아갈 색이다.
        public static void SetEnabled(Button button, bool enabled, Color accent)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
            var color = enabled ? accent : Disabled;
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = color;
            }

            var text = LabelOf(button);
            if (text != null)
            {
                text.color = color;
            }
        }

        public static void Height(Component target, float height)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.gameObject.AddComponent<LayoutElement>();
            }

            element.preferredHeight = height;
            element.minHeight = height;
        }

        public static RectTransform Scroll(RectTransform parent, string name, Vector2 min, Vector2 max, float spacing, out ScrollRect scroll)
        {
            var root = Rect(parent, name, min, max);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = true;

            var viewport = Rect(root, "Viewport", Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        // 장비 아이콘: 스프라이트가 있으면 쓰고, 없으면 등급 색 사각형 + 슬롯 이름.
        public static RectTransform GearIcon(Transform parent, string name, GearData gear, Vector2 min, Vector2 max)
        {
            var rarity = gear != null ? gear.Rarity : ItemRarity.Normal;
            var color = RarityColor(rarity);
            var frame = Panel(parent, name, min, max, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 1f), color);
            if (gear != null && gear.Icon != null)
            {
                var icon = Panel(frame.transform, "Icon", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Color.white);
                icon.sprite = gear.Icon;
                icon.preserveAspect = true;
            }
            else
            {
                Panel(frame.transform, "Swatch", new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.86f), color);
                Label(frame.transform, "Slot", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0.04f), new Vector2(1f, 0.4f), Color.white,
                    gear != null ? SlotLabel(gear.Slot) : "?");
            }

            return frame.rectTransform;
        }

        // 체크 표시를 막대 두 개로 그린다(픽셀 폰트에 ✓ 글리프가 없을 수 있음).
        public static RectTransform CheckMark(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var root = Rect(parent, name, min, max);
            var ring = root.gameObject.AddComponent<Image>();
            ring.color = new Color(color.r, color.g, color.b, 0.18f);
            ring.raycastTarget = false;
            var outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(3f, -3f);

            var shortBar = Panel(root, "Short", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), color).rectTransform;
            shortBar.sizeDelta = new Vector2(10f, 30f);
            shortBar.anchoredPosition = new Vector2(-12f, -6f);
            shortBar.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var longBar = Panel(root, "Long", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), color).rectTransform;
            longBar.sizeDelta = new Vector2(10f, 54f);
            longBar.anchoredPosition = new Vector2(8f, 2f);
            longBar.localRotation = Quaternion.Euler(0f, 0f, -40f);
            return root;
        }

        // 씬의 달러 아이콘(Dollor_Panel/DollarIcon)을 빌려 쓴다. 없으면 null → 금색 사각형.
        public static Sprite DollarSprite()
        {
            if (_dollarSearched)
            {
                return _dollarSprite;
            }

            _dollarSearched = true;
            var images = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null && (images[i].name == "DollarIcon" || images[i].name == "CostIcon"))
                {
                    _dollarSprite = images[i].sprite;
                    break;
                }
            }

            return _dollarSprite;
        }

        public static Image DollarIcon(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var sprite = DollarSprite();
            var image = Panel(parent, name, min, max, sprite != null ? Color.white : Palette.Gold);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }
    }
}
