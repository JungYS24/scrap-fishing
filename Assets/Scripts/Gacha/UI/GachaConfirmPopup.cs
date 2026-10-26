using System;
using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 뽑기 확인 팝업: 배너 그림(2:1로 위아래를 잘라 폭 전체), 카드에서 잘린 설명 전체, 안내(환원가·환원율·확률 보간·확률), 달러 전→후, 뽑기/취소.
    // 화면 틀은 씬에 있을 수 있어(Reuse) 배치는 여기서 다시 맞춘다.
    public sealed class GachaConfirmPopup
    {
        public const string Disclaimer = "표시된 확률은 이 배너 기준이며, 천장 보정이 적용되면 레전드 확률이 올라갑니다";

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly RectTransform _panel;
        readonly Image _bannerPanel;
        readonly Text _bannerText;
        readonly RectTransform _showcase;
        readonly Image _art;
        readonly Text _desc;
        readonly Text _title;
        readonly Text _info;
        readonly Text _disclaimer;
        readonly Text _balance;
        readonly Text _warning;
        readonly Button _confirm;
        readonly Action<GachaBanner, int> _onConfirm;
        GachaBanner _banner;
        int _count;

        public bool IsOpen => _root.gameObject.activeSelf;
        public Image Art => _art;
        public Text Description => _desc;

        public GachaConfirmPopup(RectTransform parent, Action<GachaBanner, int> onConfirm)
        {
            _onConfirm = onConfirm;
            _root = GachaUi.Panel(parent, "GachaConfirm", Vector2.zero, new Vector2(1f, 0.925f), GachaUi.Dim, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            _panel = Place(GachaUi.Panel(_root, "Panel", PanelMin, PanelMax, GachaUi.Card, Palette.NeonGreen, true).rectTransform, PanelMin, PanelMax);
            _title = Place(GachaUi.Label(_panel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.905f), new Vector2(0.96f, 0.985f), Palette.Text),
                new Vector2(0.04f, 0.905f), new Vector2(0.96f, 0.985f));

            // 배너 그림: 폭 전체, 2:1로 위아래를 잘라 보여 준다(4:3 그림을 칸에 꽉 채우고 넘치는 위아래는 RectMask2D로 자름).
            var bannerMin = new Vector2(0.04f, 0.62f);
            var bannerMax = new Vector2(0.96f, 0.895f);
            _bannerPanel = Place(GachaUi.Panel(_panel, "Banner", bannerMin, bannerMax, GachaUi.Inset, Palette.Magenta), bannerMin, bannerMax);
            GachaUi.GetOrAdd<RectMask2D>(_bannerPanel);
            _art = GachaUi.Panel(_bannerPanel.transform, "Art", Vector2.zero, Vector2.one, Color.white);
            _art.preserveAspect = true;
            var envelope = GachaUi.GetOrAdd<AspectRatioFitter>(_art);
            envelope.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            envelope.aspectRatio = 4f / 3f;
            _art.transform.SetAsFirstSibling();
            var captionFade = GachaUi.Panel(_bannerPanel.transform, "CaptionFade", Vector2.zero, new Vector2(1f, 0.35f), new Color(0f, 0f, 0f, 0.8f));
            captionFade.sprite = BannerArt.FadeUp;
            captionFade.transform.SetSiblingIndex(1);
            _bannerText = GachaUi.Label(_bannerPanel.transform, "BannerText", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Palette.Text);
            _showcase = GachaUi.Rect(_bannerPanel.transform, "Showcase", new Vector2(0.7f, 0.08f), new Vector2(0.98f, 0.92f));

            // 카드에서 2줄로 잘린 설명 전체.
            _desc = GachaUi.Label(_panel, "Desc", 14, TextAnchor.UpperLeft, new Vector2(0.06f, 0.535f), new Vector2(0.94f, 0.612f), new Color(0.88f, 0.92f, 1f, 1f));
            _desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            _desc.resizeTextForBestFit = true;
            _desc.resizeTextMinSize = 11;
            _desc.resizeTextMaxSize = 14;

            _info = Place(GachaUi.Label(_panel, "Info", 16, TextAnchor.UpperLeft, new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.525f), Palette.Text),
                new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.525f));
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            _info.lineSpacing = 1.15f;
            _info.resizeTextForBestFit = true;
            _info.resizeTextMinSize = 12;
            _info.resizeTextMaxSize = 16;

            // 씬에 있던 문구는 Show에서 배너별 문구로 덮어쓴다.
            _disclaimer = Place(GachaUi.Label(_panel, "Disclaimer", 16, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.215f), new Vector2(0.97f, 0.265f), GachaUi.Muted, Disclaimer),
                new Vector2(0.03f, 0.215f), new Vector2(0.97f, 0.265f));
            _disclaimer.horizontalOverflow = HorizontalWrapMode.Wrap;
            _disclaimer.resizeTextForBestFit = true;
            _disclaimer.resizeTextMinSize = 11;
            _disclaimer.resizeTextMaxSize = 16;

            _balance = Place(GachaUi.Label(_panel, "Balance", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.155f), new Vector2(0.96f, 0.212f), Palette.Text),
                new Vector2(0.04f, 0.155f), new Vector2(0.96f, 0.212f));
            _warning = Place(GachaUi.Label(_panel, "Warning", 16, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.11f), new Vector2(0.96f, 0.152f), Palette.Magenta,
                "달러가 부족합니다. 출항해서 벌어 오세요!"), new Vector2(0.04f, 0.11f), new Vector2(0.96f, 0.152f));

            Place((RectTransform)GachaUi.MakeButton(_panel, "Cancel", "취소", new Vector2(0.05f, 0.02f), new Vector2(0.4f, 0.1f), GachaUi.Muted, 22, Hide).transform,
                new Vector2(0.05f, 0.02f), new Vector2(0.4f, 0.1f));
            _confirm = GachaUi.MakeButton(_panel, "Confirm", "뽑기", new Vector2(0.44f, 0.02f), new Vector2(0.95f, 0.1f), Palette.NeonGreen, 22, Confirm);
            Place((RectTransform)_confirm.transform, new Vector2(0.44f, 0.02f), new Vector2(0.95f, 0.1f));
            _root.gameObject.SetActive(false);
        }

        static readonly Vector2 PanelMin = new Vector2(0.04f, 0.03f);
        static readonly Vector2 PanelMax = new Vector2(0.96f, 0.97f);

        static T Place<T>(T target, Vector2 min, Vector2 max) where T : Component
        {
            GachaUi.Stretch((RectTransform)target.transform, min, max);
            return target;
        }

        public void Show(GachaShop shop, GachaBanner banner, int count)
        {
            _banner = banner;
            _count = count;
            var cost = GachaShop.CostOf(banner, count);
            var balance = shop.Balance;
            var afford = balance >= cost;
            var block = shop.Block(banner, count);
            var tint = GachaUi.BannerTint(banner);

            _title.text = BannerArt.Title(banner) + " · " + CountLabel(count);
            _bannerText.text = BannerCaption(shop, banner);
            _bannerText.color = tint;
            // 배너 그림이 있으면 그림 위 아래쪽에 짧은 안내만, 없으면 예전처럼 가운데 안내 + 장비 쇼케이스.
            var art = banner.BannerSprite;
            _art.sprite = art;
            _art.gameObject.SetActive(art != null);
            _bannerPanel.transform.Find("CaptionFade").gameObject.SetActive(art != null);
            GachaUi.Stretch(_bannerText.rectTransform, Vector2.zero, art != null ? new Vector2(1f, 0.22f) : Vector2.one);
            _showcase.gameObject.SetActive(art == null);
            if (art == null)
            {
                GachaUi.Showcase(_showcase, GachaUi.ShowcaseGears(shop, banner));
            }

            _desc.text = GachaShopView.DescriptionOf(banner);
            var outline = _bannerPanel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = tint;
            }

            _info.text = InfoText(shop, banner, count);
            _disclaimer.text = DisclaimerFor(banner);
            var afterColor = afford ? Palette.Text : Palette.Hazard;
            _balance.text = "잔액 " + GachaUi.Money(balance) + "  →  <color=" + GachaUi.Hex(afterColor) + ">"
                + (afford ? GachaUi.Money(balance - cost) : "-" + GachaUi.Money(cost - balance)) + "</color>";
            _warning.text = BlockText(block);
            _warning.gameObject.SetActive(block != BannerBlock.None);
            var label = GachaUi.LabelOf(_confirm);
            if (label != null)
            {
                var discount = GachaShop.DiscountPercent(banner, count);
                label.text = cost > 0
                    ? GachaUi.Money(cost) + "로 뽑기" + (discount > 0 ? " <color=" + GachaUi.Hex(Palette.Gold) + ">(" + discount + "% 할인)</color>" : string.Empty)
                    : "무료로 뽑기";
            }

            GachaUi.SetEnabled(_confirm, block == BannerBlock.None, Palette.NeonGreen);
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.DOKill(false);
            _group.DOFade(1f, 0.15f).SetUpdate(true).SetLink(_root.gameObject);
            Fx.Pop(_panel);
        }

        public void Hide()
        {
            _group.DOKill(false);
            _root.gameObject.SetActive(false);
        }

        public static string CountLabel(int count)
        {
            return count <= 1 ? "1회" : count + "연";
        }

        static string BannerCaption(GachaShop shop, GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Pickup:
                    return "PICK UP · " + GachaUi.SlotLabel(banner.Machine.Slot);
                case BannerType.Festival:
                    return "FESTIVAL · 전체 머신";
                case BannerType.Daily:
                    return "FREE · " + DailyMachineName(shop, banner);
                default:
                    return "BLACK MARKET · " + GachaUi.SlotLabel(banner.Machine.Slot);
            }
        }

        public static string DailyMachineName(GachaShop shop, GachaBanner banner)
        {
            var machine = shop.DailyMachine(banner);
            return machine != null ? machine.DisplayName : "전체 머신";
        }

        // 확률 안내 문구. 데일리·축제는 천장 보정 대신 확정 조건을 적는다.
        public static string DisclaimerFor(GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Daily:
                    return "표시된 확률은 이 배너 기준이며, " + KoreanName(banner.GuaranteeMin) + " 이상 확정입니다";
                case BannerType.Festival:
                    return "표시된 확률은 이 배너 기준이며, " + banner.PityAt + "회째 " + KoreanName(banner.PityMin) + " 이상 확정입니다";
                default:
                    return Disclaimer;
            }
        }

        static string KoreanName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return "레어";
                case ItemRarity.Epic: return "에픽";
                case ItemRarity.Legend: return "레전드";
                case ItemRarity.Synchro: return "싱크로";
                default: return "노멀";
            }
        }

        public static string BlockText(BannerBlock block)
        {
            switch (block)
            {
                case BannerBlock.DailyDone: return "오늘은 이미 받았어요. 내일 05:00(KST)에 갱신됩니다";
                case BannerBlock.LimitReached: return "오늘 남은 횟수가 부족합니다. 매일 05:00(KST)에 갱신됩니다";
                case BannerBlock.NotOffered: return "이 배너에서는 이 횟수로 뽑을 수 없습니다";
                case BannerBlock.NoDollars: return "달러가 부족합니다. 출항해서 벌어 오세요!";
                default: return string.Empty;
            }
        }

        static string InfoText(GachaShop shop, GachaBanner banner, int count)
        {
            var counter = Mathf.Max(0, shop.Counter(banner));
            var rates = new float[GachaService.RarityCount];
            var dailyMachine = banner.Type == BannerType.Daily ? shop.DailyMachine(banner) : null;
            GachaService.Rates(banner, counter, true, rates, dailyMachine ?? shop.Catalog.Machines[0]);
            var floor = banner.Type == BannerType.Daily ? banner.GuaranteeMin : ItemRarity.Normal;

            var pool = banner.Machine != null ? banner.Machine.Gears : dailyMachine != null ? dailyMachine.Gears : shop.Catalog.AllGears;
            var refundMin = int.MaxValue;
            var refundMax = 0;
            foreach (var gear in pool)
            {
                if (gear == null || gear.Rarity < floor)
                {
                    continue;
                }

                var value = shop.Catalog.RefundValue(gear);
                refundMin = Mathf.Min(refundMin, value);
                refundMax = Mathf.Max(refundMax, value);
            }

            if (refundMin == int.MaxValue)
            {
                refundMin = 0;
            }

            var lines = "· 환원가 " + GachaUi.Money(refundMin) + " ~ " + GachaUi.Money(refundMax) + " (등급별)\n";
            if (banner.Type != BannerType.Daily)
            {
                var refundRate = banner.Type == BannerType.Standard
                    ? GachaService.ExpectedRefundRate(banner.Machine)
                    : GachaService.ExpectedRefundRate(banner, shop.Catalog);
                lines += "· 총 환원율 약 " + Mathf.RoundToInt(refundRate * 100f) + "% (등급별 환원가 기댓값)\n";
            }

            switch (banner.Type)
            {
                case BannerType.Standard:
                case BannerType.Pickup:
                    lines += "· 확률 보간: 지금 Legend " + GachaUi.Percent(rates[(int)ItemRarity.Legend]) + ", " + banner.Machine.BoostStart
                        + "회째부터 상승 (천장 없음, 상시 머신과 카운트 공유)\n";
                    break;
                case BannerType.Festival:
                    lines += "· " + (banner.DailyLimit ? "오늘 남은 횟수 " : "남은 횟수 ") + shop.FestivalRemaining(banner) + "/" + banner.MaxPulls
                        + (shop.FestivalHadHigh(banner) ? " · 레전드 이상 획득함\n" : " · " + banner.PityAt + "회째에 레전드 이상 확정\n");
                    break;
                case BannerType.Daily:
                    lines += "· 오늘의 머신: " + DailyMachineName(shop, banner) + " (매일 05:00 KST 갱신)\n";
                    break;
            }

            lines += "· 확률 " + RateLine(rates, floor);
            if (banner.Type == BannerType.Pickup && banner.Featured != null)
            {
                lines += "\n" + PickupLine(banner, rates);
            }

            var guarantee = banner.Type == BannerType.Daily ? banner.GuaranteeMin : GachaService.GuaranteeFor(count);
            if (guarantee != ItemRarity.Normal)
            {
                lines += "\n· <color=" + GachaUi.Hex(GachaUi.RarityColor(guarantee)) + ">" + GachaUi.RarityLabel(guarantee)
                    + (banner.Type == BannerType.Daily ? " 이상 확정</color>" : " 이상 1개 확정</color>");
            }

            return lines;
        }

        // 픽업 장비 확률과 같은 등급 나머지 장비 확률.
        static string PickupLine(GachaBanner banner, float[] rates)
        {
            var rarity = banner.Featured.Rarity;
            var gradeRate = rates[(int)rarity];
            var share = GachaService.FeaturedShareOf(banner);
            var others = 0;
            foreach (var gear in banner.Machine.Gears)
            {
                if (gear != null && gear.Rarity == rarity && gear != banner.Featured)
                {
                    others++;
                }
            }

            var color = GachaUi.Hex(GachaUi.RarityColor(rarity));
            var line = "· <color=" + color + ">픽업 " + banner.Featured.DisplayName + " " + GachaUi.Percent(gradeRate * share) + "</color>";
            if (others > 0)
            {
                line += " · 다른 " + GachaUi.RarityLabel(rarity) + " " + GachaUi.Percent(gradeRate * (1f - share));
            }

            if (banner.Pickup == PickupKind.Synchro)
            {
                line += " · LEGEND 2종 각 " + GachaUi.Percent(rates[(int)ItemRarity.Legend] * 0.5f);
            }

            return line;
        }

        // floor 아래 등급을 빼고 다시 나눈 실제 확률.
        static string RateLine(float[] rates, ItemRarity floor)
        {
            var total = 0f;
            for (var r = (int)floor; r < GachaService.RarityCount; r++)
            {
                total += Mathf.Max(0f, rates[r]);
            }

            var line = string.Empty;
            for (var r = (int)floor; r < GachaService.RarityCount; r++)
            {
                var rarity = (ItemRarity)r;
                var share = total > 0f ? rates[r] / total : 0f;
                if (rarity == ItemRarity.Synchro && share <= 0f)
                {
                    continue;
                }

                line += (r > (int)floor ? " · " : string.Empty) + "<color=" + GachaUi.Hex(GachaUi.RarityColor(rarity)) + ">"
                    + GachaUi.RarityLabel(rarity)[0] + " " + GachaUi.Percent(share) + "</color>";
            }

            return line;
        }

        void Confirm()
        {
            if (_banner == null || !_confirm.interactable)
            {
                return;
            }

            _onConfirm?.Invoke(_banner, _count);
        }
    }
}
