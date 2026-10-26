using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 오리파·보관함 화면 공통 스킨 부품. 화면은 예전처럼 GachaUi로 만든 뒤 여기서 그림만 바꿔 입힌다.
    // 스킨(Resources/UI/OripaSkin)이 없거나 그 이름의 스프라이트가 없으면 아무것도 바꾸지 않아 예전 단색 모양 그대로 돈다.
    public static class OripaLook
    {
        public const float PressedTint = 0.8f;

        static readonly string[] RaritySuffix = { "normal", "rare", "epic", "legend", "synchro" };
        static readonly Color DisabledText = new Color(0.55f, 0.58f, 0.64f, 1f);

        public static bool On => OripaSkin.Load() != null;

        public static Sprite Get(string name)
        {
            var skin = OripaSkin.Load();
            return skin != null ? skin.Get(name) : null;
        }

        // row_epic, icon_slot_legend 처럼 등급이 붙는 이름.
        public static string Of(string prefix, ItemRarity rarity)
        {
            var index = Mathf.Clamp((int)rarity, 0, RaritySuffix.Length - 1);
            return prefix + "_" + RaritySuffix[index];
        }

        // Image를 스프라이트로 바꾼다. 9-slice(테두리 있음)면 Sliced + 칸 높이에 맞춘 테두리 두께(SlicedFit), 없으면 Simple + 비율 유지.
        // 예전 선 테두리(Outline)는 끈다. 스프라이트가 없으면 false(그대로 둠).
        public static bool Apply(Image image, string name, bool fillCenter = true, Color? tint = null)
        {
            var sprite = Get(name);
            if (image == null || sprite == null)
            {
                return false;
            }

            var sliced = sprite.border != Vector4.zero;
            if (sliced)
            {
                SlicedFit.Apply(image, sprite);
                image.fillCenter = fillCenter;
            }
            else
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            image.color = tint ?? Color.white;
            HideOutline(image);
            return true;
        }

        // 그림 한 장을 새로 깐다(글자 뒤 띠·판 등). 스킨이 없으면 null.
        public static Image Layer(Transform parent, string objectName, string sprite, Vector2 min, Vector2 max, Color? tint = null, bool fillCenter = true)
        {
            if (Get(sprite) == null)
            {
                return null;
            }

            var existing = parent.Find(objectName);
            var image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                image = GachaUi.Panel(parent, objectName, min, max, Color.white);
            }

            GachaUi.Stretch(image.rectTransform, min, max);
            image.raycastTarget = false;
            Apply(image, sprite, fillCenter, tint);
            return image;
        }

        // 글자 뒤에 판을 깐다(글자보다 먼저 그려지게 바로 앞 형제로).
        public static Image Behind(Graphic front, string sprite, Vector2? min = null, Vector2? max = null, Color? tint = null)
        {
            if (front == null)
            {
                return null;
            }

            var rect = front.rectTransform;
            var image = Layer(rect.parent, front.name + "Plate", sprite, min ?? rect.anchorMin, max ?? rect.anchorMax, tint);
            if (image != null)
            {
                image.transform.SetSiblingIndex(rect.GetSiblingIndex());
            }

            return image;
        }

        public static void HideOutline(Component target)
        {
            var outline = target != null ? target.GetComponent<Outline>() : null;
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        // 버튼 바탕을 스프라이트로, 눌림은 Color Tint 0.8, 글자 색은 그대로(또는 지정).
        public static bool Button(Button button, string sprite, Color? text = null)
        {
            if (button == null || !Apply(button.targetGraphic as Image, sprite))
            {
                return false;
            }

            PressTint(button);
            var label = GachaUi.LabelOf(button);
            if (label != null && text.HasValue)
            {
                label.color = text.Value;
            }

            return true;
        }

        public static void PressTint(Selectable selectable)
        {
            selectable.transition = Selectable.Transition.ColorTint;
            var colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(PressedTint, PressedTint, PressedTint, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            selectable.colors = colors;
        }

        // 켜짐/꺼짐에 따라 바탕 그림을 바꾼다(btn_pull ↔ btn_pull_disabled 등). 꺼짐이면 글자도 회색.
        public static void SwapState(Button button, bool enabled, string onSprite, string offSprite, Color onText)
        {
            if (!On || button == null)
            {
                return;
            }

            Apply(button.targetGraphic as Image, enabled ? onSprite : offSprite);
            var label = GachaUi.LabelOf(button);
            if (label != null)
            {
                label.color = enabled ? onText : DisabledText;
            }
        }

        // 한 줄로 넣고, 칸 폭을 넘으면 글자를 줄인다(최소 min). 두 줄로 쪼개지지 않는다.
        public static void FitOneLine(Text text, int max, int min)
        {
            TextOneLine.Fit(text, max, min);
        }

        // 원형 빛(glow_radial)을 등급색으로 아이콘 뒤에.
        public static Image Glow(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var glow = Layer(parent, "Glow", "glow_radial", min, max, new Color(color.r, color.g, color.b, 0.75f));
            if (glow != null)
            {
                glow.preserveAspect = true;
                glow.transform.SetAsFirstSibling();
            }

            return glow;
        }

        // 레전드·싱크로 카드 위의 반짝이(작은 사각 빛 몇 개가 커졌다 사라짐).
        public static void Sparkles(RectTransform parent, Color color, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var spark = GachaUi.Panel(parent, "Sparkle" + i, Vector2.zero, Vector2.zero, color);
                spark.raycastTarget = false;
                var rect = spark.rectTransform;
                var point = new Vector2(Random.Range(0.12f, 0.88f), Random.Range(0.35f, 0.92f));
                rect.anchorMin = rect.anchorMax = point;
                var size = Random.Range(4f, 8f);
                rect.sizeDelta = new Vector2(size, size);
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                rect.localScale = Vector3.zero;
                var delay = Random.Range(0f, 1.2f);
                DOTween.Sequence().SetUpdate(true).SetLink(spark.gameObject).SetLoops(-1)
                    .AppendInterval(delay)
                    .Append(rect.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                    .Append(rect.DOScale(0f, 0.45f).SetEase(Ease.InQuad))
                    .AppendInterval(1.2f - delay);
            }
        }

        // 체크 표시 글자(✓). 글꼴에 없으면 빈 문자열.
        public static string CheckGlyph(Text text)
        {
            return text != null && text.font != null && text.font.HasCharacter('✓') ? "✓ " : string.Empty;
        }
    }
}
