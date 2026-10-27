using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 배너 아트 카드 공통: 가독성 그라데이션 스프라이트, 제목 문구·효과.
    // 아트는 모두 같은 구도(주인공 오른쪽 60%, 왼쪽 40%는 어두운 빈 공간, 아래 20%는 어두운 띠)라 글자는 왼쪽, 버튼은 아래 띠에 둔다.
    public static class BannerArt
    {
        // 그림은 4:3(높이 = 폭 × 0.75). 카드는 아래 어두운 띠(그림 아래 20%)를 잘라 높이 = 폭 × 0.6.
        public const float ArtAspect = 0.75f;
        public const float CardAspect = 0.6f;
        // 그림을 카드 위에 맞춰 붙였을 때 그림 아래 끝의 카드 기준 y(0.6 카드에서 -0.25). 이 아래는 RectMask2D로 잘린다.
        public const float ArtBottom = 1f - ArtAspect / CardAspect;
        // 예전(0.75) 카드 기준 길이를 새 카드 기준으로 바꾸는 배율(픽셀 크기 유지).
        public const float Rescale = ArtAspect / CardAspect;

        static readonly Color ShimmerLegend = new Color(1f, 0.78f, 0.45f, 1f);
        static readonly Color Dark = new Color(0f, 0f, 0f, 0.85f);

        static Sprite _fadeRight;
        static Sprite _fadeUp;
        static Sprite _band;

        // 카드·확인 팝업이 모두 이 문구를 쓴다. 언어는 GachaBanner.Name이 번역 표(banner.<id>.name)로 고른다.
        public static string Title(GachaBanner banner)
        {
            return banner != null ? banner.Name : string.Empty;
        }

        // 왼쪽 불투명 → 오른쪽 투명(64×1). 왼쪽 그라데이션에 쓴다.
        public static Sprite FadeRight => _fadeRight != null ? _fadeRight : _fadeRight = Gradient(64, 1, (x, y) => 1f - x);

        // 아래 불투명 → 위 투명(1×64). 아래 그라데이션에 쓴다.
        public static Sprite FadeUp => _fadeUp != null ? _fadeUp : _fadeUp = Gradient(1, 64, (x, y) => 1f - y);

        // 가운데가 밝은 띠(32×1). 제목 반짝임에 쓴다.
        public static Sprite Band => _band != null ? _band : _band = Gradient(32, 1, (x, y) => Mathf.Pow(Mathf.Sin(x * Mathf.PI), 2f));

        static Sprite Gradient(int width, int height, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = width > 1 ? x / (float)(width - 1) : 0f;
                    var v = height > 1 ? y / (float)(height - 1) : 0f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // 왼쪽(왼쪽 끝 검정 → 폭 55%에서 0)·아래(카드 아래 끝 검정 0.8 → 높이 35%에서 0) 그라데이션.
        // 상시 배너는 세로 영상 첫 장면을 잘라 만든 임시 그림이라 주인공이 가운데에 있어서 왼쪽을 더 진하게 깐다.
        public static void AddFades(RectTransform card, GachaBanner banner)
        {
            var left = GachaUi.Panel(card, "FadeLeft", Vector2.zero, new Vector2(0.55f, 1f), new Color(0f, 0f, 0f, banner.Type == BannerType.Standard ? 0.85f : 0.75f));
            left.sprite = FadeRight;
            var bottom = GachaUi.Panel(card, "FadeBottom", Vector2.zero, new Vector2(1f, 0.35f), new Color(0f, 0f, 0f, 0.8f));
            bottom.sprite = FadeUp;
        }

        // 제목 효과. 반환한 트윈은 카드가 화면에 보일 때만 돌린다.
        //   상시: 볼드 + 외곽선 + 그림자 / 레전드 픽업: + 주황 반짝임(3초) / 싱크로 픽업: + 하양·핑크·연두 반짝임
        //   대축제: 핑크 네온 글로우 + 밝기 맥동(2초) / 오늘의 암시장: 초록 네온 글로우
        public static void StyleTitle(Text title, GachaBanner banner, List<Tween> effects)
        {
            title.fontStyle = FontStyle.Bold;
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            outline.effectColor = Dark;
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);

            switch (banner.Type)
            {
                case BannerType.Festival:
                    Glow(outline, shadow, Palette.Magenta);
                    var bright = new Color(1f, 0.82f, 0.95f, 1f);
                    title.color = bright;
                    effects.Add(DOTween.To(() => outline.effectColor, c => outline.effectColor = c, new Color(Palette.Magenta.r, Palette.Magenta.g, Palette.Magenta.b, 0.35f), 1f)
                        .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(title.gameObject));
                    effects.Add(title.DOColor(Color.white, 1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(title.gameObject));
                    break;
                case BannerType.Daily:
                    Glow(outline, shadow, Palette.NeonGreen);
                    break;
                case BannerType.Pickup:
                    Shimmer(title, banner.Pickup == PickupKind.Synchro, effects);
                    break;
            }
        }

        static void Glow(Outline outline, Shadow shadow, Color color)
        {
            outline.effectColor = new Color(color.r, color.g, color.b, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.effectColor = new Color(color.r, color.g, color.b, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        // 글자 모양으로 가린(Mask) 비스듬한 밝은 띠가 왼쪽에서 오른쪽으로 지나간다. 셰이더 없이 글자 위에만 보인다.
        static void Shimmer(Text title, bool synchro, List<Tween> effects)
        {
            var mask = title.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var band = GachaUi.Panel(title.transform, "Shine", new Vector2(-0.4f, -0.3f), new Vector2(-0.1f, 1.3f), synchro ? Color.white : ShimmerLegend);
            band.sprite = Band;
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var colors = GachaVideoConfig.SynchroFlashColors;
            var step = 0;
            var pass = DOTween.Sequence().SetUpdate(true).SetLink(title.gameObject);
            pass.AppendCallback(() =>
            {
                var rect = band.rectTransform;
                rect.anchorMin = new Vector2(-0.4f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(-0.1f, rect.anchorMax.y);
                if (synchro)
                {
                    band.color = colors[step++ % colors.Length];
                }
            });
            pass.Append(DOTween.To(() => 0f, t =>
            {
                var rect = band.rectTransform;
                var x = Mathf.Lerp(-0.4f, 1.1f, t);
                rect.anchorMin = new Vector2(x, rect.anchorMin.y);
                rect.anchorMax = new Vector2(x + 0.3f, rect.anchorMax.y);
            }, 1f, 0.9f).SetEase(Ease.InOutSine));
            pass.AppendInterval(2.1f);
            pass.SetLoops(-1);
            effects.Add(pass);
        }

        // 카드 테두리: 레전드 픽업 #FF9A2E, 싱크로 픽업은 하양·핑크·연두 순환, 대축제 핑크, 데일리 초록, 상시는 머신 색.
        public static Outline Border(Image card, GachaBanner banner, List<Tween> effects)
        {
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            outline.effectColor = GachaUi.BannerTint(banner);
            if (banner.Type == BannerType.Pickup && banner.Pickup == PickupKind.Synchro)
            {
                var colors = GachaVideoConfig.SynchroFlashColors;
                outline.effectColor = colors[0];
                var cycle = DOTween.Sequence().SetLoops(-1).SetUpdate(true).SetLink(card.gameObject);
                for (var i = 0; i < colors.Length; i++)
                {
                    var next = colors[(i + 1) % colors.Length];
                    cycle.Append(DOTween.To(() => outline.effectColor, c => outline.effectColor = c, next, 0.4f).SetEase(Ease.InOutSine));
                }

                effects.Add(cycle);
            }

            return outline;
        }
    }
}
