using System;
using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 뽑기 확인 팝업: 배너, 안내(환원가·환원율·확률 보간·확률), 달러 전→후, 뽑기/취소.
    public sealed class GachaConfirmPopup
    {
        public const string Disclaimer = "표시된 확률은 머신 기준이며 개별 결과를 보장하지 않습니다";

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly RectTransform _panel;
        readonly Image _banner;
        readonly Text _bannerText;
        readonly Text _title;
        readonly Text _info;
        readonly Text _balance;
        readonly Text _warning;
        readonly Button _confirm;
        readonly Action<GachaMachineData, int> _onConfirm;
        GachaMachineData _machine;
        int _count;

        public bool IsOpen => _root.gameObject.activeSelf;

        public GachaConfirmPopup(RectTransform parent, Action<GachaMachineData, int> onConfirm)
        {
            _onConfirm = onConfirm;
            _root = GachaUi.Panel(parent, "GachaConfirm", Vector2.zero, new Vector2(1f, 0.925f), GachaUi.Dim, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            _panel = GachaUi.Panel(_root, "Panel", new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.9f), GachaUi.Card, Palette.NeonGreen, true).rectTransform;
            _title = GachaUi.Label(_panel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.98f), Palette.Text);

            _banner = GachaUi.Panel(_panel, "Banner", new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.88f), GachaUi.Inset, Palette.Magenta);
            _bannerText = GachaUi.Label(_banner.transform, "BannerText", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Palette.Text);

            _info = GachaUi.Label(_panel, "Info", 16, TextAnchor.UpperLeft, new Vector2(0.06f, 0.37f), new Vector2(0.94f, 0.74f), Palette.Text);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            _info.lineSpacing = 1.15f;

            GachaUi.Label(_panel, "Disclaimer", 16, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.3f), new Vector2(0.97f, 0.36f), GachaUi.Muted, Disclaimer)
                .horizontalOverflow = HorizontalWrapMode.Wrap;

            _balance = GachaUi.Label(_panel, "Balance", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.29f), Palette.Text);
            _warning = GachaUi.Label(_panel, "Warning", 16, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.2f), Palette.Magenta,
                "달러가 부족합니다. 출항해서 벌어 오세요!");

            GachaUi.MakeButton(_panel, "Cancel", "취소", new Vector2(0.05f, 0.03f), new Vector2(0.4f, 0.12f), GachaUi.Muted, 22, Hide);
            _confirm = GachaUi.MakeButton(_panel, "Confirm", "뽑기", new Vector2(0.44f, 0.03f), new Vector2(0.95f, 0.12f), Palette.NeonGreen, 22, Confirm);
            _root.gameObject.SetActive(false);
        }

        public void Show(GachaShop shop, GachaMachineData machine, int count)
        {
            _machine = machine;
            _count = count;
            var cost = GachaShop.CostOf(machine, count);
            var balance = shop.Balance;
            var afford = balance >= cost;
            var counter = shop.Inventory.Counter(machine.Id);

            _title.text = machine.DisplayName + " · " + CountLabel(count);
            _bannerText.text = "BLACK MARKET · " + GachaUi.SlotLabel(machine.Slot);
            _bannerText.color = GachaUi.SlotTint(machine.Slot);
            var outline = _banner.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = GachaUi.SlotTint(machine.Slot);
            }

            _info.text = InfoText(machine, counter, count);
            var afterColor = afford ? Palette.Text : Palette.Hazard;
            _balance.text = "잔액 " + GachaUi.Money(balance) + "  →  <color=" + GachaUi.Hex(afterColor) + ">"
                + (afford ? GachaUi.Money(balance - cost) : "-" + GachaUi.Money(cost - balance)) + "</color>";
            _warning.gameObject.SetActive(!afford);
            var label = GachaUi.LabelOf(_confirm);
            if (label != null)
            {
                label.text = GachaUi.Money(cost) + "로 뽑기";
            }

            GachaUi.SetEnabled(_confirm, afford, Palette.NeonGreen);
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

        static string InfoText(GachaMachineData machine, int counter, int count)
        {
            var catalogMin = int.MaxValue;
            var catalogMax = 0;
            foreach (var gear in machine.Gears)
            {
                if (gear == null)
                {
                    continue;
                }

                var value = Mathf.RoundToInt(machine.Price * gear.RefundMultiplier);
                catalogMin = Mathf.Min(catalogMin, value);
                catalogMax = Mathf.Max(catalogMax, value);
            }

            if (catalogMin == int.MaxValue)
            {
                catalogMin = 0;
            }

            var legendNow = GachaService.LegendRate(machine, counter);
            var lines = "· 환원가 " + GachaUi.Money(catalogMin) + " ~ " + GachaUi.Money(catalogMax) + " (등급별)\n"
                + "· 총 환원율 약 " + Mathf.RoundToInt(GachaService.ExpectedRefundRate(machine) * 100f) + "% (등급별 환원가 기댓값)\n"
                + "· 확률 보간: 지금 Legend " + GachaUi.Percent(legendNow) + ", " + machine.BoostStart + "회째부터 상승 (천장 없음)\n"
                + "· 확률 " + RateLine(machine);
            var guarantee = GachaService.GuaranteeFor(count);
            if (guarantee != ItemRarity.Normal)
            {
                lines += "\n· <color=" + GachaUi.Hex(GachaUi.RarityColor(guarantee)) + ">" + GachaUi.RarityLabel(guarantee) + " 이상 1개 확정</color>";
            }

            return lines;
        }

        static string RateLine(GachaMachineData machine)
        {
            var total = 0f;
            for (var r = 0; r < GachaService.RarityCount; r++)
            {
                total += Mathf.Max(0f, machine.BaseRate((ItemRarity)r));
            }

            var line = string.Empty;
            for (var r = 0; r < GachaService.RarityCount; r++)
            {
                var rarity = (ItemRarity)r;
                var share = total > 0f ? machine.BaseRate(rarity) / total : 0f;
                if (rarity == ItemRarity.Synchro && share <= 0f)
                {
                    continue;
                }

                line += (r > 0 ? " · " : string.Empty) + "<color=" + GachaUi.Hex(GachaUi.RarityColor(rarity)) + ">"
                    + GachaUi.RarityLabel(rarity)[0] + " " + Mathf.RoundToInt(share * 100f) + "%</color>";
            }

            return line;
        }

        void Confirm()
        {
            if (_machine == null || !_confirm.interactable)
            {
                return;
            }

            _onConfirm?.Invoke(_machine, _count);
        }
    }
}
