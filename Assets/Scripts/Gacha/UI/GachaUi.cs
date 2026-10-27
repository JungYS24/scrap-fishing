using System;
using System.Collections.Generic;
using ScrapFishing.Audio;
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
        public static readonly Color FisherColor = new Color(1f, 0.45f, 0.3f);
        public static readonly Color RodColor = new Color(0.62f, 0.72f, 1f);
        // Outline은 메시를 테두리 색으로 한 번 더 그리므로, 테두리가 있는 패널은 불투명해야 색이 번지지 않는다.
        public static readonly Color Backdrop = new Color(0.035f, 0.055f, 0.11f, 1f);
        public static readonly Color Card = new Color(0.06f, 0.09f, 0.17f, 1f);
        public static readonly Color Inset = new Color(0.03f, 0.04f, 0.08f, 1f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color Muted = new Color(0.62f, 0.68f, 0.78f);
        public static readonly Color Disabled = new Color(0.35f, 0.38f, 0.44f);

        static Sprite _dollarSprite;
        static bool _dollarSearched;
        static int _reuseDepth;

        // 이 범위 안에서는 Rect·Panel·Label·MakeButton·Scroll·CheckMark가 같은 이름의 자식을 먼저 찾아 그대로 쓴다.
        // 하이러키에 배치해 둔 화면은 위치·색·문구를 씬 값 그대로 두고, 없는 오브젝트만 코드 기본값으로 만든다.
        public static IDisposable Reuse()
        {
            _reuseDepth++;
            return new ReuseScope();
        }

        sealed class ReuseScope : IDisposable
        {
            bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _reuseDepth--;
            }
        }

        static T Existing<T>(Transform parent, string name) where T : Component
        {
            if (_reuseDepth <= 0 || parent == null)
            {
                return null;
            }

            var child = parent.Find(name);
            return child != null ? child.GetComponent<T>() : null;
        }

        public static T GetOrAdd<T>(Component target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.gameObject.AddComponent<T>();
        }

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
                case GearSlot.Boat: return Loc.T("ui.slot.boat");
                case GearSlot.Drone: return Loc.T("ui.slot.drone");
                case GearSlot.Helmet: return Loc.T("ui.slot.helmet");
                case GearSlot.Fisher: return Loc.T("ui.slot.fisher");
                case GearSlot.Rod: return Loc.T("ui.slot.rod");
                default: return Loc.T("ui.slot.lure");
            }
        }

        public static Color SlotTint(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Boat: return Palette.Cyan;
                case GearSlot.Drone: return Palette.Magenta;
                case GearSlot.Helmet: return Palette.Gold;
                case GearSlot.Fisher: return FisherColor;
                case GearSlot.Rod: return RodColor;
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
            var existing = Existing<RectTransform>(parent, name);
            if (existing != null)
            {
                return existing;
            }

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
            var existing = Existing<Image>(parent, name);
            if (existing != null)
            {
                return existing;
            }

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
            var existing = Existing<Text>(parent, name);
            if (existing != null)
            {
                return existing;
            }

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
            var existing = Existing<Button>(parent, name);
            if (existing != null)
            {
                existing.onClick.AddListener(() =>
                {
                    AudioManager.Ensure().PlayUiTap();
                    onClick?.Invoke();
                });
                return existing;
            }

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

        // 슬롯 탭. 탭 수·글자는 슬롯 목록(데이터)을 따르므로, 씬에 있던 탭이어도 글자·가로 배치·크기를 코드가 다시 맞춘다.
        public static Button SlotTab(RectTransform bar, int index, int count, GearSlot? slot, Action onClick)
        {
            var min = new Vector2(index / (float)count + 0.005f, 0f);
            var max = new Vector2((index + 1) / (float)count - 0.005f, 1f);
            Func<string> label = () => slot.HasValue ? SlotLabel(slot.Value) : Loc.T("ui.common.all");
            var size = count >= 7 ? 18 : 22;
            var button = MakeButton(bar, "Tab" + index, label(), min, max, Muted, size, onClick);
            Stretch((RectTransform)button.transform, min, max);
            var text = LabelOf(button);
            if (text != null)
            {
                text.fontSize = size;
                // 영어 탭 이름(Drone, Helmet…)이 칸보다 길면 글자를 줄인다.
                TextOneLine.Fit(text, size, 12);
                LocText.Bind(text, label);
            }

            return button;
        }

        // 탭 스킨: 고른 탭 tab_on, 나머지 tab_off(스킨이 없으면 그대로).
        public static void StyleTab(Button tab, bool active)
        {
            if (tab != null && OripaLook.Apply(tab.targetGraphic as Image, active ? "tab_on" : "tab_off"))
            {
                OripaLook.PressTint(tab);
            }
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
            var existing = Existing<ScrollRect>(parent, name);
            if (existing != null && existing.content != null)
            {
                scroll = existing;
                return existing.content;
            }

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
            // 스킨이 있으면 등급별 아이콘 칸(icon_slot_*) 그림을 쓴다.
            OripaLook.Apply(frame, OripaLook.Of("icon_slot", rarity));
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

        // 머신의 최고 등급 장비(싱크로·레전드) 아이콘을 가로로 늘어놓는다. 머신 카드와 뽑기 확인 팝업에 쓴다.
        public static void Showcase(RectTransform parent, GachaMachineData machine, int count = 3)
        {
            Showcase(parent, machine != null ? TopGears(machine.Gears, count) : new List<GearData>(), count);
        }

        public static void Showcase(RectTransform parent, IList<GearData> picks, int count = 3)
        {
            Clear(parent);
            if (picks == null)
            {
                return;
            }

            count = Mathf.Max(count, picks.Count);
            for (var i = 0; i < picks.Count; i++)
            {
                var min = new Vector2(1f - (i + 1) / (float)count + 0.03f, 0f);
                var max = new Vector2(1f - i / (float)count - 0.03f, 1f);
                var icon = GearIcon(parent, "Gear" + i, picks[i], min, max);
                var fit = icon.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1f;
            }
        }

        // 높은 등급부터 count개.
        public static List<GearData> TopGears(IReadOnlyList<GearData> gears, int count)
        {
            var picks = new List<GearData>();
            for (var r = GachaService.RarityCount - 1; r >= 0 && picks.Count < count; r--)
            {
                foreach (var gear in gears)
                {
                    if (gear != null && (int)gear.Rarity == r && picks.Count < count)
                    {
                        picks.Add(gear);
                    }
                }
            }

            return picks;
        }

        // 배너 쇼케이스: 상시는 머신 최고 등급 3개, 픽업은 픽업 장비, 축제는 머신별 싱크로 3개, 데일리는 오늘 머신(일요일은 전체).
        public static List<GearData> ShowcaseGears(GachaShop shop, GachaBanner banner, int count = 3)
        {
            switch (banner.Type)
            {
                case BannerType.Pickup:
                    return new List<GearData> { banner.Featured };
                case BannerType.Daily:
                    var today = shop.DailyMachine(banner);
                    return TopGears(today != null ? today.Gears : shop.Catalog.AllGears, count);
                case BannerType.Festival:
                    return TopGears(shop.Catalog.AllGears, count);
                default:
                    return TopGears(banner.Machine.Gears, count);
            }
        }

        // 배너 테두리·글자 색. 픽업은 레전드 색, 싱크로 픽업은 하양(카드에서 하양·핑크·연두로 번갈아 바뀐다).
        public static Color BannerTint(GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Pickup:
                    return banner.Pickup == PickupKind.Synchro ? SynchroColor : LegendColor;
                case BannerType.Festival:
                    return Palette.Magenta;
                case BannerType.Daily:
                    return Palette.NeonGreen;
                default:
                    return SlotTint(banner.Machine.Slot);
            }
        }

        // 체크 표시를 막대 두 개로 그린다(픽셀 폰트에 ✓ 글리프가 없을 수 있음).
        public static RectTransform CheckMark(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var existing = Existing<RectTransform>(parent, name);
            if (existing != null)
            {
                return existing;
            }

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
