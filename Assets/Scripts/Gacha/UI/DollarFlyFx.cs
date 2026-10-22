using System;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.FX;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 누른 버튼 위치에서 달러 아이콘이 튀어나와 흩어진 뒤 잔액 패널로 빨려 들어간다.
    // 움직임과 타이밍은 HUD 칩 연출(ChipFlyFx)과 같은 UiFxSettings 값을 쓴다.
    public static class DollarFlyFx
    {
        public static int CoinCount(UiFxSettings settings, int amount)
        {
            var min = settings != null ? settings.minChips : 3;
            var max = settings != null ? settings.mutantChips : 10;
            var byValue = 3 + Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1, amount)) * 1.5f);
            var count = Mathf.Clamp(byValue, min + 1, max + 2);
            if (settings != null && settings.Reduced)
            {
                count = Mathf.Clamp((count + 1) / 2, settings.reducedMinChips, settings.MaxChips);
            }

            return count;
        }

        public static float Play(RectTransform layer, RectTransform from, RectTransform target, int amount, UiFxSettings settings, Action firstArrive, Action complete)
        {
            if (layer == null || from == null || target == null || amount <= 0)
            {
                firstArrive?.Invoke();
                complete?.Invoke();
                return 0f;
            }

            var count = CoinCount(settings, amount);
            var size = settings != null ? settings.chipSize : 34f;
            var pop = settings != null ? settings.popDuration : 0.15f;
            var scatter = settings != null ? settings.scatterDuration : 0.16f;
            var distance = (settings != null ? settings.scatterDistance : 46f) * 1.4f;
            var hold = settings != null ? settings.holdDuration : 0.07f;
            var fly = (settings != null ? settings.flyDuration : 0.4f) * 1.35f;
            var stagger = settings != null ? settings.stagger : 0.045f;
            var punch = settings != null ? settings.arrivePunch : 0.18f;

            var start = (Vector2)layer.InverseTransformPoint(from.TransformPoint(from.rect.center));
            var end = (Vector2)layer.InverseTransformPoint(target.TransformPoint(target.rect.center));
            var sprite = GachaUi.DollarSprite();
            var arrived = 0;
            var total = 0f;
            for (var i = 0; i < count; i++)
            {
                var coin = new GameObject("DollarCoin", typeof(RectTransform)).GetComponent<RectTransform>();
                coin.SetParent(layer, false);
                coin.anchorMin = coin.anchorMax = new Vector2(0.5f, 0.5f);
                coin.sizeDelta = new Vector2(size, size);
                coin.localPosition = start;
                coin.localScale = Vector3.zero;
                var image = coin.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = sprite != null ? Color.white : Palette.Gold;

                var angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.35f, 0.35f);
                var spread = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance * UnityEngine.Random.Range(0.6f, 1.1f);
                var delay = i * stagger;
                var index = i;
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(coin.gameObject);
                seq.AppendInterval(delay * 0.35f);
                seq.Append(coin.DOScale(1f, pop).SetEase(Ease.OutBack));
                seq.Join(coin.DOLocalMove(spread, scatter).SetEase(Ease.OutQuad));
                seq.AppendInterval(hold + delay);
                seq.Append(coin.DOLocalMoveX(end.x, fly).SetEase(Ease.InQuad));
                seq.Join(coin.DOLocalMoveY(end.y, fly).SetEase(Ease.InBack));
                seq.Join(coin.DOScale(0.7f, fly).SetEase(Ease.InQuad));
                seq.OnComplete(() =>
                {
                    arrived++;
                    if (arrived == 1)
                    {
                        firstArrive?.Invoke();
                    }

                    AudioManager.Ensure().PlayChipArrive(1f + index / (float)Mathf.Max(1, count) * 0.4f);
                    Fx.Punch(target, punch, 0.14f);
                    UnityEngine.Object.Destroy(coin.gameObject);
                    if (arrived == count)
                    {
                        AudioManager.Ensure().PlayCountDone();
                        complete?.Invoke();
                    }
                });
                total = Mathf.Max(total, delay * 0.35f + pop + hold + delay + fly);
            }

            return total;
        }
    }
}
