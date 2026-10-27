using System;
using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 뽑기 확인 팝업: 배너 그림(2:1로 위아래를 잘라 폭 전체), 카드에서 잘린 설명 전체, 안내(환원가·환원율·확률 보간·확률), 달러 전→후, 뽑기/취소.
    // 화면 틀은 씬에 있을 수 있어(Reuse) 배치는 여기서 다시 맞춘다.
    public sealed class GachaConfirmPopup
    {
        public static string Disclaimer => Loc.T("ui.confirm.disclaimer");

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
        // 스킨: 배너 그림 위 테두리, 그림 왼쪽 아래 모서리에 걸친 태그, 안내 줄(점·구분선·등급 칩), 잔액 칸.
        readonly bool _skinned;
        readonly Image _frame;
        readonly Image _tag;
        readonly Text _tagText;
        readonly RectTransform _rows;
        readonly RectTransform _balanceRow;
        readonly Text _before;
        readonly Text _after;
        readonly Image _afterBox;
        GachaBanner _banner;
        GachaShop _shop;
        int _count;

        public bool IsOpen => _root.gameObject.activeSelf;
        public Image Art => _art;
        public Text Description => _desc;
        public Button ConfirmButton => _confirm;
        public RectTransform Rows => _rows;
        public Image Tag => _tag;

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
                BlockText(BannerBlock.NoDollars)), new Vector2(0.04f, 0.11f), new Vector2(0.96f, 0.152f));

            var cancel = GachaUi.MakeButton(_panel, "Cancel", string.Empty, new Vector2(0.05f, 0.02f), new Vector2(0.4f, 0.1f), GachaUi.Muted, 22, Hide);
            Place((RectTransform)cancel.transform, new Vector2(0.05f, 0.02f), new Vector2(0.4f, 0.1f));
            LocText.Bind(cancel, "ui.common.cancel");
            _confirm = GachaUi.MakeButton(_panel, "Confirm", Loc.T("ui.confirm.pull"), new Vector2(0.44f, 0.02f), new Vector2(0.95f, 0.1f), Palette.NeonGreen, 22, Confirm);
            Place((RectTransform)_confirm.transform, new Vector2(0.44f, 0.02f), new Vector2(0.95f, 0.1f));
            // "$87,000로 뽑기 / 3% 할인"이 버튼 밖으로 넘치지 않게 두 줄 + 자동 축소(최소 16px).
            var confirmLabel = GachaUi.LabelOf(_confirm);
            confirmLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            confirmLabel.resizeTextForBestFit = true;
            confirmLabel.resizeTextMinSize = 16;
            confirmLabel.resizeTextMaxSize = 22;
            confirmLabel.lineSpacing = 0.85f;

            // 배너 태그: 그림 왼쪽 아래 모서리에 걸친다(그림 가운데를 가리지 않게). 그림 칸의 마스크 밖이라 잘리지 않는다.
            _tag = GachaUi.Panel(_panel, "BannerTag", new Vector2(0.06f, 0.598f), new Vector2(0.52f, 0.644f), GachaUi.Inset, Palette.Magenta);
            _tagText = GachaUi.Label(_tag.transform, "Text", 16, TextAnchor.MiddleCenter, new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), Palette.Text);
            _tag.gameObject.SetActive(false);

            _skinned = OripaLook.Apply(_panel.GetComponent<Image>(), "popup_cyan");
            // 설명은 그림 왼쪽 아래 모서리의 태그 밑으로.
            GachaUi.Stretch(_desc.rectTransform, new Vector2(0.06f, 0.536f), new Vector2(0.94f, 0.593f));
            if (!_skinned)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            OripaLook.Behind(_title, "popup_header_cyan", new Vector2(0.12f, 0.905f), new Vector2(0.88f, 0.982f));
            OripaLook.HideOutline(_bannerPanel);
            _frame = OripaLook.Layer(_bannerPanel.transform, "Frame", "banner_frame_standard", Vector2.zero, Vector2.one, null, false);
            OripaLook.Behind(_info, "info_box", new Vector2(0.04f, 0.262f), new Vector2(0.96f, 0.532f));
            _info.gameObject.SetActive(false);
            _rows = GachaUi.Rect(_panel, "InfoRows", new Vector2(0.065f, 0.274f), new Vector2(0.935f, 0.52f));

            // 잔액: value_box → arrow_right → 뽑은 뒤(줄어들면 value_box, 늘어나면 value_box_gain).
            _balance.gameObject.SetActive(false);
            _balanceRow = GachaUi.Rect(_panel, "BalanceRow", new Vector2(0.06f, 0.152f), new Vector2(0.94f, 0.212f));
            var beforeBox = OripaLook.Layer(_balanceRow, "Before", "value_box", Vector2.zero, new Vector2(0.42f, 1f));
            _before = GachaUi.Label(beforeBox.transform, "Text", 22, TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), Palette.Text);
            OripaLook.Layer(_balanceRow, "Arrow", "arrow_right", new Vector2(0.44f, 0.1f), new Vector2(0.56f, 0.9f));
            _afterBox = OripaLook.Layer(_balanceRow, "After", "value_box", new Vector2(0.58f, 0f), Vector2.one);
            _after = GachaUi.Label(_afterBox.transform, "Text", 22, TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), Palette.Text);
            TextOneLine.Fit(_before, 22, 14);
            TextOneLine.Fit(_after, 22, 14);

            OripaLook.Button(GachaUi.LabelOf(_confirm).GetComponentInParent<Button>(), "btn_green", Palette.Text);
            OripaLook.Button(_panel.Find("Cancel").GetComponent<Button>(), "btn_gray", Palette.Text);
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
            _shop = shop;
            _count = count;
            Fill(shop, banner, count);
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.DOKill(false);
            _group.DOFade(1f, 0.15f).SetUpdate(true).SetLink(_root.gameObject);
            Fx.Pop(_panel);
        }

        // 열려 있는 동안 언어가 바뀌면 같은 배너·횟수로 글자를 다시 채운다.
        public void Relabel()
        {
            if (IsOpen && _banner != null && _shop != null)
            {
                Fill(_shop, _banner, _count);
            }
        }

        void Fill(GachaShop shop, GachaBanner banner, int count)
        {
            var cost = GachaShop.CostOf(banner, count);
            var balance = shop.Balance;
            var afford = balance >= cost;
            var block = shop.Block(banner, count);
            var tint = GachaUi.BannerTint(banner);

            _title.text = BannerArt.Title(banner) + " · " + CountLabel(count);
            _bannerText.text = BannerCaption(shop, banner);
            _bannerText.color = tint;
            // 배너 그림이 있으면 안내는 그림 왼쪽 아래 모서리의 태그로(그림 가운데를 가리지 않게), 없으면 예전처럼 가운데 안내 + 장비 쇼케이스.
            var art = banner.BannerSprite;
            _art.sprite = art;
            _art.gameObject.SetActive(art != null);
            _bannerPanel.transform.Find("CaptionFade").gameObject.SetActive(false);
            _bannerText.gameObject.SetActive(art == null);
            _tag.gameObject.SetActive(art != null);
            _tagText.text = BannerCaption(shop, banner);
            _tagText.color = tint;
            TextOneLine.Fit(_tagText, 16, 11);
            if (banner.Type == BannerType.Festival)
            {
                OripaLook.Apply(_tag, "tag_plate_pink");
            }
            else
            {
                OripaLook.Apply(_tag, "tag_plate_white", true, tint);
            }

            if (_frame != null)
            {
                OripaLook.Apply(_frame, GachaShopView.FrameSprite(banner), false);
                _frame.gameObject.SetActive(art != null);
            }

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
            if (_skinned)
            {
                BuildRows(shop, banner, count);
            }

            _disclaimer.text = DisclaimerFor(banner);
            var afterColor = afford ? Palette.Text : Palette.Hazard;
            _balance.text = Loc.F("ui.confirm.balance", GachaUi.Money(balance), "<color=" + GachaUi.Hex(afterColor) + ">"
                + (afford ? GachaUi.Money(balance - cost) : "-" + GachaUi.Money(cost - balance)) + "</color>");
            if (_skinned)
            {
                _before.text = GachaUi.Money(balance);
                _after.text = afford ? GachaUi.Money(balance - cost) : "-" + GachaUi.Money(cost - balance);
                _after.color = afterColor;
                OripaLook.Apply(_afterBox, cost < 0 ? "value_box_gain" : "value_box");
                TextOneLine.Fit(_before, 22, 14);
                TextOneLine.Fit(_after, 22, 14);
            }
            _warning.text = BlockText(block);
            _warning.gameObject.SetActive(block != BannerBlock.None);
            var label = GachaUi.LabelOf(_confirm);
            if (label != null)
            {
                var discount = GachaShop.DiscountPercent(banner, count);
                label.text = cost > 0
                    ? Loc.F("ui.confirm.pull_for", GachaUi.Money(cost))
                        + (discount > 0 ? "\n<size=14><color=" + GachaUi.Hex(Palette.Gold) + ">" + Loc.F("ui.confirm.discount", discount) + "</color></size>" : string.Empty)
                    : Loc.T("ui.confirm.pull_free");
            }

            GachaUi.SetEnabled(_confirm, block == BannerBlock.None, Palette.NeonGreen);
        }

        public void Hide()
        {
            _group.DOKill(false);
            _root.gameObject.SetActive(false);
        }

        public static string CountLabel(int count)
        {
            return count <= 1 ? Loc.T("ui.common.pull_once") : Loc.F("ui.common.pull_n", count);
        }

        static string BannerCaption(GachaShop shop, GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Pickup:
                    return "PICK UP · " + GachaUi.SlotLabel(banner.Machine.Slot);
                case BannerType.Festival:
                    return Loc.T("ui.confirm.caption_festival");
                case BannerType.Daily:
                    return "FREE · " + DailyMachineName(shop, banner);
                default:
                    return "BLACK MARKET · " + GachaUi.SlotLabel(banner.Machine.Slot);
            }
        }

        public static string DailyMachineName(GachaShop shop, GachaBanner banner)
        {
            var machine = shop.DailyMachine(banner);
            return machine != null ? machine.DisplayName : Loc.T("ui.confirm.all_machines");
        }

        // 확률 안내 문구. 데일리·축제는 천장 보정 대신 확정 조건을 적는다.
        public static string DisclaimerFor(GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Daily:
                    return Loc.F("ui.confirm.disclaimer_daily", GradeName(banner.GuaranteeMin));
                case BannerType.Festival:
                    return Loc.F("ui.confirm.disclaimer_festival", banner.PityAt, GradeName(banner.PityMin));
                default:
                    return Disclaimer;
            }
        }

        // 문장 속 등급 이름(레어·Rare). 칩·태그의 대문자 표기(RARE)는 GachaUi.RarityLabel.
        public static string GradeName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return Loc.T("ui.grade.rare");
                case ItemRarity.Epic: return Loc.T("ui.grade.epic");
                case ItemRarity.Legend: return Loc.T("ui.grade.legend");
                case ItemRarity.Synchro: return Loc.T("ui.grade.synchro");
                default: return Loc.T("ui.grade.normal");
            }
        }

        public static string BlockText(BannerBlock block)
        {
            switch (block)
            {
                case BannerBlock.DailyDone: return Loc.T("ui.confirm.block_daily_done");
                case BannerBlock.LimitReached: return Loc.T("ui.confirm.block_limit");
                case BannerBlock.NotOffered: return Loc.T("ui.confirm.block_count");
                case BannerBlock.NoDollars: return Loc.T("ui.confirm.block_money");
                default: return string.Empty;
            }
        }

        // 안내 줄(확률 줄 제외)과 줄 앞 점 색. 확률은 rates(floor 아래 등급은 빼고 다시 나눈 값)로 따로 그린다.
        static List<(string Text, Color Dot)> InfoLines(GachaShop shop, GachaBanner banner, int count, float[] rates, out ItemRarity floor)
        {
            var counter = Mathf.Max(0, shop.Counter(banner));
            var dailyMachine = banner.Type == BannerType.Daily ? shop.DailyMachine(banner) : null;
            GachaService.Rates(banner, counter, true, rates, dailyMachine ?? shop.Catalog.Machines[0]);
            floor = banner.Type == BannerType.Daily ? banner.GuaranteeMin : ItemRarity.Normal;

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

            var lines = new List<(string, Color)> { (Loc.F("ui.confirm.refund_range", GachaUi.Money(refundMin), GachaUi.Money(refundMax)), Palette.Gold) };
            if (banner.Type != BannerType.Daily)
            {
                var refundRate = banner.Type == BannerType.Standard
                    ? GachaService.ExpectedRefundRate(banner.Machine)
                    : GachaService.ExpectedRefundRate(banner, shop.Catalog);
                lines.Add((Loc.F("ui.confirm.refund_rate", Mathf.RoundToInt(refundRate * 100f)), Palette.Cyan));
            }

            switch (banner.Type)
            {
                case BannerType.Standard:
                case BannerType.Pickup:
                    lines.Add((Loc.F("ui.confirm.pity_line", GachaUi.Percent(rates[(int)ItemRarity.Legend]), banner.Machine.BoostStart), GachaUi.LegendColor));
                    break;
                case BannerType.Festival:
                    lines.Add((Loc.F(banner.DailyLimit ? "ui.confirm.festival_left_today" : "ui.confirm.festival_left", shop.FestivalRemaining(banner), banner.MaxPulls)
                        + (shop.FestivalHadHigh(banner) ? Loc.T("ui.confirm.festival_got_high") : Loc.F("ui.confirm.festival_pity", banner.PityAt)), Palette.Magenta));
                    break;
                case BannerType.Daily:
                    lines.Add((Loc.F("ui.confirm.daily_machine", DailyMachineName(shop, banner)), Palette.NeonGreen));
                    break;
            }

            if (banner.Type == BannerType.Pickup && banner.Featured != null)
            {
                lines.Add((PickupLine(banner, rates).Substring(2), GachaUi.RarityColor(banner.Featured.Rarity)));
            }

            var guarantee = banner.Type == BannerType.Daily ? banner.GuaranteeMin : GachaService.GuaranteeFor(count);
            if (guarantee != ItemRarity.Normal)
            {
                lines.Add(("<color=" + GachaUi.Hex(GachaUi.RarityColor(guarantee)) + ">"
                    + Loc.F(banner.Type == BannerType.Daily ? "ui.confirm.guarantee_daily" : "ui.confirm.guarantee_multi", GachaUi.RarityLabel(guarantee))
                    + "</color>", GachaUi.RarityColor(guarantee)));
            }

            return lines;
        }

        // 스킨이 없을 때 쓰는 예전 안내 글(줄마다 "· ", 확률 줄은 세 번째 안내 뒤).
        static string InfoText(GachaShop shop, GachaBanner banner, int count)
        {
            var rates = new float[GachaService.RarityCount];
            var lines = InfoLines(shop, banner, count, rates, out var floor);
            var text = string.Empty;
            var typeLine = banner.Type == BannerType.Daily ? 2 : 3;
            for (var i = 0; i < lines.Count; i++)
            {
                text += (i > 0 ? "\n" : string.Empty) + "· " + lines[i].Text;
                if (i == typeLine - 1)
                {
                    text += "\n· " + Loc.T("ui.confirm.rate_prefix") + RateLine(rates, floor);
                }
            }

            return text;
        }

        // 스킨: info_box 안에 한 줄씩(앞에 등급색 점, 줄 사이 1px 구분선), 확률은 등급 칩 5개를 가로로 놓고 칩 아래 숫자.
        void BuildRows(GachaShop shop, GachaBanner banner, int count)
        {
            GachaUi.Clear(_rows);
            var rates = new float[GachaService.RarityCount];
            var lines = InfoLines(shop, banner, count, rates, out var floor);
            const float chipUnits = 2.2f;
            var units = lines.Count + chipUnits;
            var top = 1f;
            for (var i = 0; i < lines.Count; i++)
            {
                var bottom = top - 1f / units;
                var row = GachaUi.Rect(_rows, "Line" + i, new Vector2(0f, bottom), new Vector2(1f, top));
                var dot = GachaUi.Panel(row, "Dot", new Vector2(0f, 0.38f), new Vector2(0f, 0.62f), lines[i].Dot);
                dot.rectTransform.sizeDelta = new Vector2(6f, 0f);
                dot.rectTransform.anchoredPosition = new Vector2(4f, 0f);
                var text = GachaUi.Label(row, "Text", 14, TextAnchor.MiddleLeft, new Vector2(0f, 0f), Vector2.one, Palette.Text, lines[i].Text);
                text.rectTransform.offsetMin = new Vector2(14f, 0f);
                TextOneLine.Fit(text, 14, 10);
                var line = GachaUi.Panel(row, "Divider", Vector2.zero, new Vector2(1f, 0f), new Color(1f, 1f, 1f, 0.12f));
                line.rectTransform.sizeDelta = new Vector2(0f, 1f);
                top = bottom;
            }

            // 확률 칩: floor 아래 등급(데일리의 노멀 등)은 빼고 다시 나눈 확률. 싱크로 확률이 0이면 칩을 흐리게.
            var chips = GachaUi.Rect(_rows, "Rates", Vector2.zero, new Vector2(1f, top));
            var total = 0f;
            for (var r = (int)floor; r < GachaService.RarityCount; r++)
            {
                total += Mathf.Max(0f, rates[r]);
            }

            for (var r = 0; r < GachaService.RarityCount; r++)
            {
                var rarity = (ItemRarity)r;
                var share = r >= (int)floor && total > 0f ? rates[r] / total : 0f;
                var min = new Vector2(r / (float)GachaService.RarityCount + 0.01f, 0.36f);
                var max = new Vector2((r + 1) / (float)GachaService.RarityCount - 0.01f, 0.98f);
                var chip = OripaLook.Layer(chips, "Chip_" + rarity, OripaLook.Of("chip", rarity), min, max, share > 0f ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                if (chip != null)
                {
                    chip.preserveAspect = true;
                }

                var label = GachaUi.Label(chips, "Rate_" + rarity, 14, TextAnchor.MiddleCenter, new Vector2(min.x, 0f), new Vector2(max.x, 0.36f),
                    share > 0f ? GachaUi.RarityColor(rarity) : GachaUi.Muted, GachaUi.Percent(share));
                TextOneLine.Fit(label, 14, 10);
            }
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
            var line = "· <color=" + color + ">" + Loc.F("ui.confirm.pickup", banner.Featured.DisplayName, GachaUi.Percent(gradeRate * share)) + "</color>";
            if (others > 0)
            {
                line += Loc.F("ui.confirm.pickup_others", GachaUi.RarityLabel(rarity), GachaUi.Percent(gradeRate * (1f - share)));
            }

            if (banner.Pickup == PickupKind.Synchro)
            {
                line += Loc.F("ui.confirm.pickup_legend_each", GachaUi.Percent(rates[(int)ItemRarity.Legend] * 0.5f));
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
