using System;
using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    public struct RefundPick
    {
        public GearData Gear;
        public int Count;
    }

    // 뽑기 결과: 등급별 묶음(높은 등급 먼저), 등급별 합계 $, 카드마다 선택, 전체 선택/초기화, 선택 합계, 달러 환원/보관함으로.
    // 같은 장비는 한 카드로 묶고 개수만큼 선택할 수 있다. 이미 보유한 장비는 전부, 새 장비는 1개를 남기고 기본 선택한다.
    public sealed class GachaResultView
    {
        sealed class Stack
        {
            public GearData Gear;
            public int Count;
            public int Selected;
            public bool OwnedBefore;
            public Text SelectText;
            public Image Box;
            public Image BoxFill;
        }

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly Text _title;
        readonly RectTransform _list;
        readonly ScrollRect _scroll;
        readonly Text _selection;
        readonly Button _refund;
        readonly Action<List<RefundPick>, RectTransform> _onRefund;
        readonly Action<List<RefundPick>> _onStore;
        readonly Action _onClose;
        readonly List<Stack> _stacks = new List<Stack>();
        readonly List<Text> _groupTotals = new List<Text>();
        GachaShop _shop;

        public GachaMachineData Machine { get; private set; }
        public bool IsOpen => _root.gameObject.activeSelf;

        public GachaResultView(RectTransform parent, Action<List<RefundPick>, RectTransform> onRefund, Action<List<RefundPick>> onStore, Action onClose)
        {
            _onRefund = onRefund;
            _onStore = onStore;
            _onClose = onClose;
            _root = GachaUi.Panel(parent, "GachaResult", Vector2.zero, new Vector2(1f, 0.925f), GachaUi.Backdrop, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            _title = GachaUi.Label(_root, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.92f), new Vector2(0.72f, 0.99f), Palette.Text);
            GachaUi.MakeButton(_root, "Close", "닫기", new Vector2(0.74f, 0.925f), new Vector2(0.95f, 0.985f), GachaUi.Muted, 22, () => _onClose?.Invoke());

            _list = GachaUi.Scroll(_root, "List", new Vector2(0.03f, 0.235f), new Vector2(0.97f, 0.915f), 6f, out _scroll);

            GachaUi.MakeButton(_root, "SelectAll", "전체 선택", new Vector2(0.05f, 0.17f), new Vector2(0.48f, 0.225f), Palette.Cyan, 22, SelectAll);
            GachaUi.MakeButton(_root, "Reset", "초기화", new Vector2(0.52f, 0.17f), new Vector2(0.95f, 0.225f), GachaUi.Muted, 22, ResetSelection);
            _selection = GachaUi.Label(_root, "Selection", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.11f), new Vector2(0.96f, 0.165f), Palette.Gold);
            _refund = GachaUi.MakeButton(_root, "Refund", "달러 환원", new Vector2(0.05f, 0.02f), new Vector2(0.48f, 0.1f), Palette.Gold, 22, RequestRefund);
            GachaUi.MakeButton(_root, "Store", "보관함으로", new Vector2(0.52f, 0.02f), new Vector2(0.95f, 0.1f), Palette.NeonGreen, 22, RequestStore);
            _root.gameObject.SetActive(false);
        }

        public void Show(GachaShop shop, GachaPullResult result)
        {
            _shop = shop;
            Machine = result.Machine;
            _title.text = result.Machine.DisplayName + " " + GachaConfirmPopup.CountLabel(result.Count) + " 결과";
            BuildStacks(result);
            Rebuild();
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.DOKill(false);
            _group.DOFade(1f, 0.18f).SetUpdate(true).SetLink(_root.gameObject);
            _scroll.verticalNormalizedPosition = 1f;
            if (_stacks.Count > 0 && _stacks[0].Gear.Rarity >= ItemRarity.Legend)
            {
                AudioManager.Ensure().PlayLegendAppear();
            }
        }

        public void Hide()
        {
            _group.DOKill(false);
            _root.gameObject.SetActive(false);
        }

        void BuildStacks(GachaPullResult result)
        {
            _stacks.Clear();
            var byId = new Dictionary<string, Stack>();
            foreach (var item in result.Items)
            {
                if (!byId.TryGetValue(item.Gear.Id, out var stack))
                {
                    stack = new Stack { Gear = item.Gear, OwnedBefore = item.Duplicate };
                    byId.Add(item.Gear.Id, stack);
                    _stacks.Add(stack);
                }

                stack.Count++;
            }

            foreach (var stack in _stacks)
            {
                stack.Selected = DefaultSelection(stack);
            }

            _stacks.Sort((a, b) =>
            {
                var byRarity = b.Gear.Rarity.CompareTo(a.Gear.Rarity);
                return byRarity != 0 ? byRarity : string.CompareOrdinal(a.Gear.Id, b.Gear.Id);
            });
        }

        static int DefaultSelection(Stack stack)
        {
            return stack.OwnedBefore ? stack.Count : stack.Count - 1;
        }

        void Rebuild()
        {
            GachaUi.Clear(_list);
            _groupTotals.Clear();
            var index = 0;
            for (var r = GachaService.RarityCount - 1; r >= 0; r--)
            {
                var rarity = (ItemRarity)r;
                var count = 0;
                foreach (var stack in _stacks)
                {
                    if (stack.Gear.Rarity == rarity)
                    {
                        count += stack.Count;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                var header = GachaUi.Rect(_list, "Group" + rarity, Vector2.zero, Vector2.one);
                GachaUi.Height(header, 40f);
                var color = GachaUi.RarityColor(rarity);
                GachaUi.Panel(header, "Bar", new Vector2(0f, 0.1f), new Vector2(0.015f, 0.9f), color);
                GachaUi.Label(header, "Name", 22, TextAnchor.MiddleLeft, new Vector2(0.04f, 0f), new Vector2(0.55f, 1f), color,
                    GachaUi.RarityLabel(rarity) + "  x" + count);
                var total = GachaUi.Label(header, "Total", 16, TextAnchor.MiddleRight, new Vector2(0.5f, 0f), new Vector2(0.99f, 1f), GachaUi.Muted);
                total.name = "Total" + r;
                _groupTotals.Add(total);
                Animate(header, index++);

                foreach (var stack in _stacks)
                {
                    if (stack.Gear.Rarity == rarity)
                    {
                        Animate(BuildCard(stack), index++);
                    }
                }
            }

            RefreshSelection();
        }

        RectTransform BuildCard(Stack stack)
        {
            var color = GachaUi.RarityColor(stack.Gear.Rarity);
            var card = GachaUi.Panel(_list, "Card_" + stack.Gear.Id, Vector2.zero, Vector2.one, GachaUi.Card, color, true);
            GachaUi.Height(card, 92f);
            var toggle = card.gameObject.AddComponent<Button>();
            toggle.targetGraphic = card;
            toggle.onClick.AddListener(() =>
            {
                AudioManager.Ensure().PlayUiTap();
                stack.Selected = stack.Selected >= stack.Count ? 0 : stack.Count;
                RefreshSelection();
            });

            var rect = card.rectTransform;
            GachaUi.GearIcon(rect, "Icon", stack.Gear, new Vector2(0.02f, 0.1f), new Vector2(0.18f, 0.9f));
            // 윗줄: 이름 + 보유 중 태그, 아랫줄: 개수·개당 환원가 + 선택 개수 조절, 오른쪽: 체크 칸.
            GachaUi.Label(rect, "Name", 22, TextAnchor.MiddleLeft, new Vector2(0.21f, 0.52f), new Vector2(0.71f, 0.94f), Palette.Text, stack.Gear.DisplayName);
            var refund = _shop.RefundValue(stack.Gear.Id);
            GachaUi.Label(rect, "Detail", 16, TextAnchor.MiddleLeft, new Vector2(0.21f, 0.08f), new Vector2(0.6f, 0.48f), GachaUi.Muted,
                "x" + stack.Count + " · 개당 " + GachaUi.Money(refund));
            if (stack.OwnedBefore)
            {
                var tag = GachaUi.Panel(rect, "OwnedTag", new Vector2(0.72f, 0.58f), new Vector2(0.86f, 0.9f), new Color(0.3f, 0.05f, 0.25f, 1f), Palette.Magenta);
                GachaUi.Label(tag.transform, "Text", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Palette.Magenta, "보유 중");
            }

            stack.Box = GachaUi.Panel(rect, "Box", new Vector2(0.89f, 0.3f), new Vector2(0.97f, 0.7f), GachaUi.Inset, color);
            stack.BoxFill = GachaUi.Panel(stack.Box.transform, "Fill", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), color);
            if (stack.Count > 1)
            {
                GachaUi.MakeButton(rect, "Minus", "-", new Vector2(0.61f, 0.08f), new Vector2(0.68f, 0.46f), Palette.Cyan, 22, () => Step(stack, -1));
                stack.SelectText = GachaUi.Label(rect, "Selected", 16, TextAnchor.MiddleCenter, new Vector2(0.68f, 0.08f), new Vector2(0.79f, 0.46f), Palette.Text);
                GachaUi.MakeButton(rect, "Plus", "+", new Vector2(0.79f, 0.08f), new Vector2(0.86f, 0.46f), Palette.Cyan, 22, () => Step(stack, 1));
            }

            return rect;
        }

        void Step(Stack stack, int delta)
        {
            stack.Selected = Mathf.Clamp(stack.Selected + delta, 0, stack.Count);
            RefreshSelection();
        }

        void SelectAll()
        {
            foreach (var stack in _stacks)
            {
                stack.Selected = stack.Count;
            }

            RefreshSelection();
        }

        void ResetSelection()
        {
            foreach (var stack in _stacks)
            {
                stack.Selected = 0;
            }

            RefreshSelection();
        }

        void RefreshSelection()
        {
            var pieces = 0;
            var dollars = 0;
            var groupDollars = new int[GachaService.RarityCount];
            foreach (var stack in _stacks)
            {
                var each = _shop.RefundValue(stack.Gear.Id);
                pieces += stack.Selected;
                dollars += each * stack.Selected;
                groupDollars[(int)stack.Gear.Rarity] += each * stack.Count;
                if (stack.SelectText != null)
                {
                    stack.SelectText.text = stack.Selected + "/" + stack.Count;
                }

                if (stack.BoxFill != null)
                {
                    stack.BoxFill.gameObject.SetActive(stack.Selected > 0);
                    var fill = stack.BoxFill.color;
                    fill.a = stack.Selected >= stack.Count ? 1f : 0.45f;
                    stack.BoxFill.color = fill;
                }
            }

            foreach (var total in _groupTotals)
            {
                var r = total.name[total.name.Length - 1] - '0';
                total.text = "합계 " + GachaUi.Money(groupDollars[r]);
            }

            _selection.text = pieces > 0 ? "선택 " + pieces + "개 · 환원 " + GachaUi.Money(dollars) : "환원할 장비를 고르세요";
            GachaUi.SetEnabled(_refund, pieces > 0, Palette.Gold);
        }

        List<RefundPick> Picks(bool selected)
        {
            var picks = new List<RefundPick>();
            foreach (var stack in _stacks)
            {
                var count = selected ? stack.Selected : stack.Count;
                if (count > 0)
                {
                    picks.Add(new RefundPick { Gear = stack.Gear, Count = count });
                }
            }

            return picks;
        }

        void RequestRefund()
        {
            var picks = Picks(true);
            if (picks.Count > 0)
            {
                _onRefund?.Invoke(picks, (RectTransform)_refund.transform);
            }
        }

        void RequestStore()
        {
            _onStore?.Invoke(Picks(false));
        }

        static void Animate(RectTransform rect, int index)
        {
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var delay = Mathf.Min(index, 12) * 0.05f;
            group.DOFade(1f, 0.18f).SetDelay(delay).SetUpdate(true).SetLink(rect.gameObject);
        }
    }
}
