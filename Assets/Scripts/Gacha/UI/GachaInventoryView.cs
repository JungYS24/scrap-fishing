using System;
using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 보관함: 슬롯별 장착 칸, 장착 효과 합계, 슬롯 탭, 장비 카드(적용·해제·환원).
    // 적용하면 바로 저장하고 GearBonuses를 갱신한다. 효과는 다음 출항부터 반영된다.
    public sealed class GachaInventoryView
    {
        const float ConfirmWindow = 2f;

        static readonly GearSlot?[] Tabs = { null, GearSlot.Lure, GearSlot.Boat, GearSlot.Drone, GearSlot.Fisher, GearSlot.Suit };

        sealed class Row
        {
            public GearData Gear;
            public bool Equipped;
            public int Stored;
            public int Unprocessed;
        }

        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly RectTransform _slots;
        readonly Text _summary;
        readonly RectTransform _list;
        readonly ScrollRect _scroll;
        readonly Text _empty;
        readonly Button[] _tabButtons = new Button[Tabs.Length];
        readonly Action<int> _onRefunded;
        GachaShop _shop;
        GearSlot? _filter;
        string _armedRefund;
        float _armedUntil;

        public bool IsOpen => _root.gameObject.activeSelf;

        // onRefunded: 환원으로 받은 달러. 오리파 화면의 잔액 표시를 갱신하는 데 쓴다.
        public GachaInventoryView(RectTransform parent, Action<int> onRefunded)
        {
            _onRefunded = onRefunded;
            _root = GachaUi.Panel(parent, "GachaInventory", Vector2.zero, new Vector2(1f, 0.925f), GachaUi.Backdrop, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            GachaUi.Label(_root, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.925f), new Vector2(0.7f, 0.99f), Palette.Text, "보관함");
            GachaUi.MakeButton(_root, "Close", "닫기", new Vector2(0.74f, 0.928f), new Vector2(0.95f, 0.985f), GachaUi.Muted, 22, Hide);

            GachaUi.Label(_root, "SlotsCaption", 16, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.915f), GachaUi.Muted,
                "장착 중 · 다음 출항부터 반영");
            _slots = GachaUi.Rect(_root, "Slots", new Vector2(0.03f, 0.745f), new Vector2(0.97f, 0.875f));
            _summary = GachaUi.Label(_root, "Summary", 16, TextAnchor.UpperLeft, new Vector2(0.04f, 0.645f), new Vector2(0.96f, 0.74f), Palette.Cyan);
            _summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            _summary.verticalOverflow = VerticalWrapMode.Truncate;

            var tabs = GachaUi.Rect(_root, "Tabs", new Vector2(0.03f, 0.59f), new Vector2(0.97f, 0.635f));
            for (var i = 0; i < Tabs.Length; i++)
            {
                var slot = Tabs[i];
                var min = new Vector2(i / (float)Tabs.Length + 0.005f, 0f);
                var max = new Vector2((i + 1) / (float)Tabs.Length - 0.005f, 1f);
                _tabButtons[i] = GachaUi.MakeButton(tabs, "Tab" + i, slot.HasValue ? GachaUi.SlotLabel(slot.Value) : "전체", min, max, GachaUi.Muted, 22,
                    () => SelectTab(slot));
            }

            _list = GachaUi.Scroll(_root, "List", new Vector2(0.03f, 0.015f), new Vector2(0.97f, 0.58f), 6f, out _scroll);
            _empty = GachaUi.Label(_root, "Empty", 22, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.35f), GachaUi.Muted,
                "보관 중인 장비가 없어요");
            _root.gameObject.SetActive(false);
        }

        public void Show(GachaShop shop)
        {
            _shop = shop;
            _armedRefund = null;
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.DOKill(false);
            _group.DOFade(1f, 0.15f).SetUpdate(true).SetLink(_root.gameObject);
            SelectTab(_filter);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            _group.DOKill(false);
            _root.gameObject.SetActive(false);
        }

        void SelectTab(GearSlot? slot)
        {
            _filter = slot;
            for (var i = 0; i < Tabs.Length; i++)
            {
                var active = Tabs[i] == slot;
                var accent = active ? (slot.HasValue ? GachaUi.SlotTint(slot.Value) : Palette.NeonGreen) : GachaUi.Muted;
                GachaUi.SetEnabled(_tabButtons[i], true, accent);
                if (_tabButtons[i].targetGraphic is Image image)
                {
                    image.color = active ? new Color(accent.r * 0.22f, accent.g * 0.22f, accent.b * 0.22f, 1f) : GachaUi.Inset;
                }
            }

            Refresh();
        }

        void Refresh()
        {
            if (_shop == null)
            {
                return;
            }

            GearBonuses.Refresh(_shop.Inventory);
            BuildSlots();
            _summary.text = Summary();
            GachaUi.Clear(_list);
            var rows = Rows();
            foreach (var row in rows)
            {
                BuildCard(row);
            }

            _empty.gameObject.SetActive(rows.Count == 0);
        }

        void BuildSlots()
        {
            GachaUi.Clear(_slots);
            for (var s = 0; s < GearInventory.SlotCount; s++)
            {
                var slot = (GearSlot)s;
                var gear = _shop.Inventory.Equipped(slot);
                var min = new Vector2(s / (float)GearInventory.SlotCount + 0.008f, 0f);
                var max = new Vector2((s + 1) / (float)GearInventory.SlotCount - 0.008f, 1f);
                var cell = GachaUi.Panel(_slots, "Slot_" + slot, min, max, GachaUi.Inset, gear != null ? GachaUi.RarityColor(gear.Rarity) : GachaUi.Muted).rectTransform;
                GachaUi.Label(cell, "Slot", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0.78f), new Vector2(1f, 1f), GachaUi.SlotTint(slot), GachaUi.SlotLabel(slot));
                if (gear != null)
                {
                    GachaUi.GearIcon(cell, "Icon", gear, new Vector2(0.18f, 0.24f), new Vector2(0.82f, 0.78f));
                    var name = GachaUi.Label(cell, "Name", 16, TextAnchor.MiddleCenter, new Vector2(0.02f, 0f), new Vector2(0.98f, 0.24f), Palette.Text, gear.DisplayName);
                    name.horizontalOverflow = HorizontalWrapMode.Overflow;
                    name.resizeTextForBestFit = true;
                    name.resizeTextMinSize = 10;
                    name.resizeTextMaxSize = 16;
                }
                else
                {
                    GachaUi.Label(cell, "Empty", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0.2f), new Vector2(1f, 0.75f), GachaUi.Muted, "비어 있음");
                }
            }
        }

        static string Summary()
        {
            var text = string.Empty;
            foreach (GearStat stat in Enum.GetValues(typeof(GearStat)))
            {
                var value = GearBonuses.Get(stat);
                if (Mathf.Approximately(value, 0f))
                {
                    continue;
                }

                text += (text.Length > 0 ? " · " : string.Empty) + GearBonuses.Describe(new GearEffect(stat, value));
            }

            return text.Length > 0 ? "총 효과  " + text : "장착한 장비가 없어요. 아래에서 적용을 눌러 장착하세요.";
        }

        // 장착·보관·미처리를 장비별로 묶는다. 높은 등급, 같은 등급이면 id 순.
        List<Row> Rows()
        {
            var byId = new Dictionary<string, Row>();
            var rows = new List<Row>();
            Row RowOf(GearData gear)
            {
                if (!byId.TryGetValue(gear.Id, out var row))
                {
                    row = new Row { Gear = gear };
                    byId.Add(gear.Id, row);
                    rows.Add(row);
                }

                return row;
            }

            var inventory = _shop.Inventory;
            foreach (var entry in inventory.List(GearState.Equipped, _filter))
            {
                RowOf(entry.Gear).Equipped = true;
            }

            foreach (var entry in inventory.List(GearState.Stored, _filter))
            {
                RowOf(entry.Gear).Stored = entry.Count;
            }

            foreach (var entry in inventory.List(GearState.Unprocessed, _filter))
            {
                RowOf(entry.Gear).Unprocessed = entry.Count;
            }

            rows.Sort((a, b) =>
            {
                var byEquip = b.Equipped.CompareTo(a.Equipped);
                if (byEquip != 0)
                {
                    return byEquip;
                }

                var byRarity = b.Gear.Rarity.CompareTo(a.Gear.Rarity);
                return byRarity != 0 ? byRarity : string.CompareOrdinal(a.Gear.Id, b.Gear.Id);
            });
            return rows;
        }

        void BuildCard(Row row)
        {
            var gear = row.Gear;
            var color = GachaUi.RarityColor(gear.Rarity);
            var card = GachaUi.Panel(_list, "Gear_" + gear.Id, Vector2.zero, Vector2.one, GachaUi.Card, row.Equipped ? Palette.NeonGreen : color, true).rectTransform;
            GachaUi.Height(card, 128f);

            GachaUi.GearIcon(card, "Icon", gear, new Vector2(0.02f, 0.12f), new Vector2(0.19f, 0.88f));
            GachaUi.Label(card, "Name", 22, TextAnchor.MiddleLeft, new Vector2(0.22f, 0.66f), new Vector2(0.74f, 0.96f), Palette.Text,
                gear.DisplayName + "  <size=16><color=" + GachaUi.Hex(color) + ">" + GachaUi.RarityLabel(gear.Rarity) + "</color></size>");
            var effects = GachaUi.Label(card, "Effects", 16, TextAnchor.UpperLeft, new Vector2(0.22f, 0.06f), new Vector2(0.74f, 0.64f), Palette.Cyan,
                GearBonuses.Describe(gear, "\n"));
            effects.horizontalOverflow = HorizontalWrapMode.Wrap;
            effects.lineSpacing = 0.9f;

            var count = row.Equipped
                ? "장착 중"
                : "보관 " + row.Stored + (row.Unprocessed > 0 ? " · 미처리 " + row.Unprocessed : string.Empty);
            GachaUi.Label(card, "Count", 16, TextAnchor.MiddleRight, new Vector2(0.74f, 0.7f), new Vector2(0.97f, 0.96f),
                row.Equipped ? Palette.NeonGreen : GachaUi.Muted, count);

            var spare = row.Stored + row.Unprocessed;
            if (row.Equipped)
            {
                GachaUi.MakeButton(card, "Unequip", "해제", new Vector2(0.77f, 0.38f), new Vector2(0.97f, 0.66f), GachaUi.Muted, 22, () => Unequip(gear.Slot));
            }
            else if (spare > 0)
            {
                GachaUi.MakeButton(card, "Equip", "적용", new Vector2(0.77f, 0.38f), new Vector2(0.97f, 0.66f), Palette.NeonGreen, 22, () => Equip(gear));
            }

            if (spare > 0)
            {
                var armed = _armedRefund == gear.Id && Time.unscaledTime < _armedUntil;
                var label = armed ? "확인" : "환원 " + GachaUi.Money(_shop.RefundValue(gear.Id));
                var refund = GachaUi.MakeButton(card, "Refund", label, new Vector2(0.75f, 0.06f), new Vector2(0.98f, 0.33f), armed ? Palette.Magenta : Palette.Gold, 16,
                    () => Refund(row));
                refund.transform.SetAsLastSibling();
            }
        }

        void Equip(GearData gear)
        {
            if (_shop.Equip(gear.Id))
            {
                AudioManager.Ensure().PlayUpgradeBuy();
            }

            _armedRefund = null;
            Refresh();
        }

        void Unequip(GearSlot slot)
        {
            _shop.Unequip(slot);
            _armedRefund = null;
            Refresh();
        }

        // 환원은 되돌릴 수 없어서 두 번 눌러야 한다. 보관 중 → 미처리 순으로 1개를 판다.
        void Refund(Row row)
        {
            var id = row.Gear.Id;
            if (_armedRefund != id || Time.unscaledTime >= _armedUntil)
            {
                _armedRefund = id;
                _armedUntil = Time.unscaledTime + ConfirmWindow;
                Refresh();
                return;
            }

            _armedRefund = null;
            var state = row.Stored > 0 ? GearState.Stored : GearState.Unprocessed;
            if (_shop.TryRefund(id, state, 1, out var dollars))
            {
                AudioManager.Ensure().PlayUpgradeBuy();
                _onRefunded?.Invoke(dollars);
            }
            else
            {
                AudioManager.Ensure().PlayUpgradeDenied();
            }

            Refresh();
        }
    }
}
