using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ChipFlyFx : MonoBehaviour
    {
        [SerializeField] UiFxSettings settings;
        [SerializeField] RectTransform layer;
        [SerializeField] RectTransform chipIcon;
        [SerializeField] RectTransform chipPanel;
        [SerializeField] CountingText counter;
        [SerializeField] Text mutantBanner;

        readonly Queue<Image> _pool = new Queue<Image>(48);
        readonly List<Image> _live = new List<Image>(48);
        Vector3 _iconScale = Vector3.one;
        int _inFlight;
        int _visual;
        int _pendingTotal;

        public void Bind(UiFxSettings fxSettings, RectTransform flyLayer, RectTransform icon, RectTransform panel, CountingText counting, Text banner)
        {
            settings = fxSettings;
            layer = flyLayer;
            chipIcon = icon;
            chipPanel = panel;
            counter = counting;
            mutantBanner = banner;
            if (chipIcon != null)
            {
                _iconScale = chipIcon.localScale;
            }

            WarmPool();
        }

        public void ResetVisual()
        {
            RecycleAll();
            _visual = 0;
            _pendingTotal = 0;
            _inFlight = 0;
            if (counter != null)
            {
                counter.Snap(0);
            }

            HideBanner();
        }

        public void Play(ScrapDefinition definition, Vector3 worldPos, int gain, int totalAfter)
        {
            if (settings == null || layer == null || definition == null)
            {
                if (counter != null)
                {
                    counter.PlayTo(totalAfter);
                }

                return;
            }

            if (gain < 0)
            {
                PlayLoss(Mathf.Abs(gain), totalAfter);
                return;
            }

            if (!TryWorldToLocal(worldPos, out var from))
            {
                counter.PlayTo(totalAfter);
                return;
            }

            var count = settings.ChipCount(definition.Grade, gain);
            var room = settings.MaxChips - _live.Count;
            if (room <= 0)
            {
                counter.PlayTo(totalAfter);
                return;
            }

            count = Mathf.Min(count, room);

            if (definition.Grade == ScrapGrade.Mutant)
            {
                ShowBanner(definition.DisplayName);
            }

            var to = TargetLocal();
            var share = gain / count;
            var remainder = gain - share * count;
            _pendingTotal = totalAfter;
            _inFlight += count;
            for (var i = 0; i < count; i++)
            {
                var amount = share + (i == count - 1 ? remainder : 0);
                Launch(from, to, i, amount, definition.Grade);
            }
        }

        public void PlayLoss(int magnitude, int totalAfter)
        {
            if (settings == null || layer == null)
            {
                if (counter != null)
                {
                    counter.PlayTo(totalAfter);
                }

                return;
            }

            if (chipPanel != null && !settings.Reduced)
            {
                chipPanel.DOKill(false);
                chipPanel.DOShakePosition(settings.shakeDuration, settings.shakeStrength, 14, 90f, false, true)
                    .SetUpdate(true)
                    .SetLink(chipPanel.gameObject);
            }

            if (!settings.Reduced)
            {
                var count = Mathf.Clamp(settings.minChips, 2, 5);
                count = Mathf.Min(count, Mathf.Max(1, settings.MaxChips - _live.Count));
                var from = TargetLocal();
                for (var i = 0; i < count; i++)
                {
                    var chip = Take();
                    var rect = chip.rectTransform;
                    rect.DOKill(false);
                    chip.DOKill(false);
                    rect.anchoredPosition = from + new Vector2(Random.Range(-12f, 12f), Random.Range(-6f, 6f));
                    rect.localScale = Vector3.one;
                    chip.color = settings.loseFlash;
                    var fall = from + new Vector2(Random.Range(-30f, 30f), -settings.loseFall);
                    var seq = DOTween.Sequence().SetUpdate(true).SetLink(chip.gameObject);
                    seq.SetDelay(i * settings.stagger);
                    seq.Append(rect.DOAnchorPos(fall, settings.flyDuration).SetEase(Ease.InQuad));
                    seq.Join(chip.DOFade(0f, settings.flyDuration));
                    seq.OnComplete(() => Recycle(chip));
                }
            }

            _pendingTotal = totalAfter;
            _visual = totalAfter;
            if (counter != null)
            {
                counter.PlayTo(totalAfter);
            }
        }

        void OnDisable()
        {
            RecycleAll();
        }

        void WarmPool()
        {
            var size = settings != null ? settings.MaxChips : 40;
            for (var i = _pool.Count; i < size; i++)
            {
                _pool.Enqueue(CreateChip());
            }
        }

        Image CreateChip()
        {
            var go = new GameObject("Chip");
            go.transform.SetParent(layer, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var size = settings != null ? settings.chipSize : 34f;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = ResolveSprite();
            image.color = settings != null ? settings.chipColor : Color.yellow;
            go.SetActive(false);
            return image;
        }

        Sprite ResolveSprite()
        {
            if (settings != null && settings.chipSprite != null)
            {
                return settings.chipSprite;
            }

            if (chipIcon != null)
            {
                var image = chipIcon.GetComponent<Image>();
                if (image != null && image.sprite != null)
                {
                    return image.sprite;
                }
            }

            return PlaceholderFactory.Circle(settings != null ? settings.chipColor : Color.yellow);
        }

        Image Take()
        {
            var chip = _pool.Count > 0 ? _pool.Dequeue() : CreateChip();
            if (chip.sprite == null)
            {
                chip.sprite = ResolveSprite();
            }

            chip.gameObject.SetActive(true);
            _live.Add(chip);
            return chip;
        }

        void Recycle(Image chip)
        {
            if (chip == null)
            {
                return;
            }

            chip.rectTransform.DOKill(false);
            chip.DOKill(false);
            chip.gameObject.SetActive(false);
            _live.Remove(chip);
            _pool.Enqueue(chip);
        }

        void RecycleAll()
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                Recycle(_live[i]);
            }

            _inFlight = 0;
            if (layer != null)
            {
                layer.DOKill(false);
            }

            if (chipIcon != null)
            {
                chipIcon.DOKill(false);
                chipIcon.localScale = _iconScale;
            }

            if (chipPanel != null)
            {
                chipPanel.DOKill(false);
            }
        }

        void Launch(Vector2 from, Vector2 to, int index, int amount, ScrapGrade grade)
        {
            var chip = Take();
            var rect = chip.rectTransform;
            rect.DOKill(false);
            chip.DOKill(false);
            rect.anchoredPosition = from;
            rect.localScale = Vector3.zero;
            chip.color = settings.chipColor;

            var scatter = from + Random.insideUnitCircle.normalized * settings.scatterDistance;
            var delay = index * settings.stagger;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(chip.gameObject);
            seq.Append(rect.DOScale(1f, settings.popDuration).SetEase(Ease.OutBack));
            seq.Join(rect.DOAnchorPos(scatter, settings.scatterDuration).SetEase(Ease.OutQuad));
            if (grade >= ScrapGrade.Rare && !settings.Reduced)
            {
                seq.Join(chip.DOColor(settings.rareFlash, 0.08f).SetLoops(4, LoopType.Yoyo));
            }

            seq.AppendInterval(settings.holdDuration);
            seq.Append(rect.DOAnchorPosX(to.x, settings.flyDuration).SetEase(Ease.InQuad));
            seq.Join(rect.DOAnchorPosY(to.y, settings.flyDuration).SetEase(Ease.InBack));
            seq.SetDelay(delay);
            seq.OnComplete(() =>
            {
                Arrive(amount);
                Recycle(chip);
            });
        }

        void Arrive(int amount)
        {
            _inFlight = Mathf.Max(0, _inFlight - 1);
            if (chipIcon != null && !settings.Reduced)
            {
                chipIcon.DOKill(false);
                chipIcon.localScale = _iconScale;
                chipIcon.DOPunchScale(Vector3.one * settings.arrivePunch, 0.14f, 4, 0.5f)
                    .SetUpdate(true)
                    .SetLink(chipIcon.gameObject);
            }

            _visual += amount;
            if (_inFlight == 0)
            {
                _visual = _pendingTotal;
            }

            if (counter != null)
            {
                counter.PlayTo(_visual);
            }

            if (!settings.Reduced || _inFlight == 0)
            {
                AudioManager.Ensure().PlayChipArrive(1f + Mathf.Min(0.28f, (settings.mutantChips - _inFlight) * 0.03f));
            }
        }

        void ShowBanner(string name)
        {
            if (mutantBanner == null)
            {
                return;
            }

            var rect = mutantBanner.rectTransform;
            mutantBanner.DOKill(false);
            rect.DOKill(false);
            mutantBanner.text = name;
            mutantBanner.gameObject.SetActive(true);
            var color = mutantBanner.color;
            color.a = 0f;
            mutantBanner.color = color;
            rect.localScale = Vector3.one * 0.6f;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(mutantBanner.gameObject);
            seq.Append(rect.DOScale(1.08f, settings.bannerInDuration).SetEase(Ease.OutBack));
            seq.Join(mutantBanner.DOFade(1f, settings.bannerInDuration));
            seq.AppendInterval(settings.bannerHoldDuration);
            seq.Append(mutantBanner.DOFade(0f, settings.bannerOutDuration));
            seq.OnComplete(HideBanner);
        }

        void HideBanner()
        {
            if (mutantBanner == null)
            {
                return;
            }

            mutantBanner.DOKill(false);
            mutantBanner.rectTransform.DOKill(false);
            mutantBanner.gameObject.SetActive(false);
        }

        bool TryWorldToLocal(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            var cam = FixedResolution.Current != null ? FixedResolution.Current.GameCamera : Camera.main;
            if (cam == null)
            {
                return false;
            }

            var screen = cam.WorldToScreenPoint(world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out local);
        }

        Vector2 TargetLocal()
        {
            var target = chipIcon != null ? chipIcon : chipPanel;
            if (target == null)
            {
                return Vector2.zero;
            }

            var screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out var local);
            return local;
        }
    }
}
