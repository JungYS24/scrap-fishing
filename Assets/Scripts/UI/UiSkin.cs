using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // Resources/UI 의 네온 UI 스프라이트를 코드로 바로 붙이는 헬퍼.
    //   buttons/ : 아이콘이 들어간 완성 버튼 (btn_boat, btn_rod, btn_battery, btn_cube, btn_fire, btn_target)
    //   frames/  : 텍스트·아이콘을 뺀 9-slice 프레임 (frame_button_teal, frame_bar_teal, frame_panel_dim, ...)
    //   panels/  : 목업 원본 크기 패널, 텍스트만 제거 (panel_battle, panel_money, panel_chat, panel_userid, ...)
    //   icons/   : 배경을 뺀 단독 아이콘 (icon_boat, icon_dollar, icon_chip, portrait, ...)
    public static class UiSkin
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string path)
        {
            if (Cache.TryGetValue(path, out var sprite))
            {
                return sprite;
            }

            sprite = Resources.Load<Sprite>("UI/" + path);
            if (sprite == null)
            {
                Debug.LogWarning("UiSkin: sprite not found: UI/" + path);
            }

            Cache[path] = sprite;
            return sprite;
        }

        // 늘려 쓰는 패널/버튼 배경. size 는 캔버스 단위(480x854 기준).
        public static Image Frame(Transform parent, string frame, Vector2 size, string name = null)
        {
            var image = CreateImage(parent, name ?? frame, Get("frames/" + frame));
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        // 원본 비율 그대로 쓰는 아이콘/버튼 이미지. scale 1 = 목업과 같은 크기.
        public static Image Icon(Transform parent, string path, float scale = 1f, string name = null)
        {
            var image = CreateImage(parent, name ?? path, Get(path));
            image.preserveAspect = true;
            image.SetNativeSize();
            image.rectTransform.sizeDelta *= scale;
            return image;
        }

        // 완성 아이콘 버튼 (예: "btn_boat").
        public static Button IconButton(Transform parent, string button, Action onClick, float scale = 1f)
        {
            var image = Icon(parent, "buttons/" + button, scale, button);
            return MakeButton(image, onClick);
        }

        // 9-slice 프레임 + 텍스트 라벨 버튼 (예: BATTLE).
        public static Button LabelButton(Transform parent, string frame, string label, Vector2 size, int fontSize, Action onClick)
        {
            var image = Frame(parent, frame, size, label);
            var text = UiFonts.CreateText(image.rectTransform, "Label", fontSize, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.text = label;
            return MakeButton(image, onClick);
        }

        static Button MakeButton(Image image, Action onClick)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        static Image CreateImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
