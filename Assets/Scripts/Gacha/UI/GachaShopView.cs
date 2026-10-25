using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.FX;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 암시장 머신 목록. 타이틀·결과 화면 위에 덮어 띄우고, 닫으면 아래 화면으로 돌아간다.
    // 확인 팝업 → (연출 없이) 결과 → 환원 확인·완료 또는 보관까지 이 화면이 흐름을 잇는다.
    // 화면 틀(BlackMarket 아래 패널·버튼·라벨)은 씬 하이러키에 두고 관리한다. 머신 카드·결과 목록만 실행 중에 만든다.
    public class GachaShopView : MonoBehaviour
    {
        const string Notice = "게임 안에서 번 달러로만 뽑습니다 · 실제 결제 없음";
        const float FlySafety = 0.6f;

        static readonly GearSlot?[] Tabs = { null, GearSlot.Lure, GearSlot.Rod, GearSlot.Boat, GearSlot.Drone, GearSlot.Fisher, GearSlot.Helmet };

        GachaShop _shop;
        UiFxSettings _settings;
        RectTransform _root;
        CanvasGroup _group;
        RectTransform _list;
        ScrollRect _scroll;
        Button[] _tabButtons;
        Button _close;
        // 타이틀 화면의 달러 패널(오리파 위에도 그대로 보인다). 환원 코인이 날아가고, 달러가 모자라면 흔들린다.
        RectTransform _dollarPanel;
        RectTransform _flyLayer;
        Image _flyBlocker;
        Text _toast;
        GachaConfirmPopup _confirm;
        GachaResultView _result;
        GachaRefundPopup _refund;
        GachaInventoryView _inventory;
        readonly Dictionary<string, float> _refundRates = new Dictionary<string, float>();
        GearSlot? _filter;
        List<RefundPick> _pendingRefund;
        bool _flying;
        float _flyDeadline;
        System.Action _flyFinish;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public GachaShop Shop => _shop;

        public void Build(RectTransform area)
        {
            _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            _shop = GachaShop.CreateDefault();
            foreach (var machine in _shop.Catalog.Machines)
            {
                _refundRates[machine.Id] = GachaService.ExpectedRefundRate(machine);
            }

            BuildLayout(area);
            GearBonuses.Refresh(_shop.Inventory);
            _root.gameObject.SetActive(false);
        }

        // area 아래에 BlackMarket이 있으면 그 하이러키를 그대로 쓰고, 빠진 오브젝트만 만든다.
        // 에디터의 Scrap Fishing > Build Oripa UI도 이 함수로 씬에 화면 틀을 만든다.
        public void BuildLayout(RectTransform area)
        {
            using (GachaUi.Reuse())
            {
                BuildShell(area);
            }
        }

        void BuildShell(RectTransform area)
        {
            _root = GachaUi.Panel(area, "BlackMarket", Vector2.zero, Vector2.one, GachaUi.Backdrop, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            GachaUi.Label(_root, "Notice", 16, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.89f), new Vector2(0.97f, 0.922f), GachaUi.Muted, Notice);
            BuildTabs();
            _list = GachaUi.Scroll(_root, "Machines", new Vector2(0.03f, 0.015f), new Vector2(0.97f, 0.828f), 10f, out _scroll);

            _result = new GachaResultView(_root, RequestRefund, StorePicks, CloseResult, PullAgain);
            _confirm = new GachaConfirmPopup(_root, ConfirmPull);
            _refund = new GachaRefundPopup(_root, ExecuteRefund, (machine, count) => _confirm.Show(_shop, machine, count), RefreshCards);
            _inventory = new GachaInventoryView(_root, HandleInventoryRefund);
            BuildTopBar();

            _flyLayer = GachaUi.Rect(_root, "FlyLayer", Vector2.zero, Vector2.one);
            _flyBlocker = GachaUi.GetOrAdd<Image>(_flyLayer);
            _flyBlocker.color = new Color(0f, 0f, 0f, 0f);
            _flyBlocker.raycastTarget = false;

            var toast = GachaUi.Panel(_root, "Toast", new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.52f), GachaUi.Inset, Palette.NeonGreen);
            _toast = GachaUi.Label(toast.transform, "Text", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Palette.NeonGreen);
            toast.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (_root == null)
            {
                return;
            }

            AudioManager.Ensure().Unlock();
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _confirm.Hide();
            _result.Hide();
            _refund.Hide();
            _inventory.Hide();
            SelectTab(_filter);
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.DOKill(false);
            _group.DOFade(1f, _settings != null ? _settings.resultsFadeDuration : 0.25f).SetUpdate(true).SetLink(_root.gameObject);
        }

        public void Close()
        {
            if (!IsOpen || _flying)
            {
                return;
            }

            _group.blocksRaycasts = false;
            _group.DOKill(false);
            _group.DOFade(0f, _settings != null ? _settings.titleHideDuration : 0.18f).SetUpdate(true).SetLink(_root.gameObject)
                .OnComplete(() => _root.gameObject.SetActive(false));
        }

        void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            var modal = _confirm.IsOpen || _result.IsOpen || _refund.IsOpen || _inventory.IsOpen || _flying;
            if (_close != null)
            {
                GachaUi.SetEnabled(_close, !modal, Palette.Magenta);
            }

            if (_flying && Time.unscaledTime > _flyDeadline)
            {
                FinishFly();
            }
        }

        void BuildTopBar()
        {
            var bar = GachaUi.Panel(_root, "TopBar", new Vector2(0f, 0.925f), Vector2.one, new Color(0.02f, 0.03f, 0.07f, 1f), Palette.Magenta, true).rectTransform;
            // 로고와 달러 패널은 없앤다. 달러는 타이틀 화면의 달러 패널이 오리파 위에 그대로 보인다.
            HideLegacy(bar, "Logo");
            HideLegacy(bar, "DollarPanel");
            GachaUi.MakeButton(bar, "Inventory", "보관함", new Vector2(0.67f, 0.14f), new Vector2(0.845f, 0.86f), Palette.NeonGreen, 22, OpenInventory);
            _close = GachaUi.MakeButton(bar, "Close", "X", new Vector2(0.86f, 0.14f), new Vector2(0.97f, 0.86f), Palette.Magenta, 28, Close);
        }

        public void SetDollarTarget(RectTransform panel)
        {
            _dollarPanel = panel;
        }

        // 씬에 남아 있을 수 있는 예전 오브젝트는 지우지 않고 숨긴다.
        static void HideLegacy(Transform parent, string name)
        {
            var legacy = parent.Find(name);
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
            }
        }

        void BuildTabs()
        {
            var bar = GachaUi.Rect(_root, "Tabs", new Vector2(0.03f, 0.838f), new Vector2(0.97f, 0.885f));
            _tabButtons = new Button[Tabs.Length];
            for (var i = 0; i < Tabs.Length; i++)
            {
                var slot = Tabs[i];
                _tabButtons[i] = GachaUi.SlotTab(bar, i, Tabs.Length, slot, () => SelectTab(slot));
            }
        }

        void SelectTab(GearSlot? slot)
        {
            _filter = slot;
            for (var i = 0; i < Tabs.Length; i++)
            {
                var active = Tabs[i] == slot;
                var accent = active ? (slot.HasValue ? GachaUi.SlotTint(slot.Value) : Palette.NeonGreen) : GachaUi.Muted;
                GachaUi.SetEnabled(_tabButtons[i], true, accent);
                var image = _tabButtons[i].targetGraphic as Image;
                if (image != null)
                {
                    image.color = active ? new Color(accent.r * 0.22f, accent.g * 0.22f, accent.b * 0.22f, 1f) : GachaUi.Inset;
                }
            }

            RefreshCards();
            _scroll.verticalNormalizedPosition = 1f;
        }

        void RefreshCards()
        {
            if (_list == null)
            {
                return;
            }

            GachaUi.Clear(_list);
            var index = 0;
            foreach (var machine in _shop.Catalog.Machines)
            {
                if (_filter.HasValue && machine.Slot != _filter.Value)
                {
                    continue;
                }

                var card = BuildCard(machine);
                var group = card.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.DOFade(1f, 0.2f).SetDelay(index++ * 0.06f).SetUpdate(true).SetLink(card.gameObject);
            }
        }

        RectTransform BuildCard(GachaMachineData machine)
        {
            var tint = GachaUi.SlotTint(machine.Slot);
            var card = GachaUi.Panel(_list, "Machine_" + machine.Id, Vector2.zero, Vector2.one, GachaUi.Card, tint, true).rectTransform;
            GachaUi.Height(card, 236f);

            var banner = machine.Banner;
            var bannerImage = GachaUi.Panel(card, "Banner", new Vector2(0.03f, 0.6f), new Vector2(0.97f, 0.96f),
                banner != null ? Color.white : new Color(tint.r * 0.2f, tint.g * 0.2f, tint.b * 0.2f, 1f), tint);
            bannerImage.sprite = banner;
            GachaUi.Label(bannerImage.transform, "Name", 28, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.35f), new Vector2(0.7f, 0.95f), Palette.Text, machine.DisplayName);
            GachaUi.Label(bannerImage.transform, "Slot", 16, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.05f), new Vector2(0.7f, 0.38f), tint,
                (string.IsNullOrEmpty(machine.BannerCopy) ? GachaUi.SlotLabel(machine.Slot) : machine.BannerCopy) + " · " + machine.Gears.Count + "종");
            GachaUi.Showcase(GachaUi.Rect(bannerImage.transform, "Showcase", new Vector2(0.62f, 0.04f), new Vector2(0.98f, 0.54f)), machine);

            var counter = _shop.Inventory.Counter(machine.Id);
            var boosting = counter + 1 >= machine.BoostStart;
            if (boosting)
            {
                Tag(bannerImage.rectTransform, "BoostTag", "확률 상승 중", GachaUi.LegendColor, new Vector2(0.66f, 0.56f), new Vector2(0.97f, 0.92f));
            }
            else if (_shop.Inventory.History.Count == 0)
            {
                Tag(bannerImage.rectTransform, "NewTag", "신규", Palette.NeonGreen, new Vector2(0.78f, 0.56f), new Vector2(0.97f, 0.92f));
            }

            GachaUi.Label(card, "Price", 22, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.46f), new Vector2(0.55f, 0.58f), Palette.Gold,
                "1회 " + GachaUi.Money(machine.Price));
            var rate = _refundRates.TryGetValue(machine.Id, out var cached) ? cached : GachaService.ExpectedRefundRate(machine);
            GachaUi.Label(card, "RefundRate", 16, TextAnchor.MiddleRight, new Vector2(0.45f, 0.46f), new Vector2(0.96f, 0.58f), Palette.Cyan,
                "총 환원율 약 " + Mathf.RoundToInt(rate * 100f) + "%");

            BuildGauge(card, machine, counter, boosting);

            var counts = new[] { 1, GachaService.TenPull, GachaService.HundredPull };
            for (var i = 0; i < counts.Length; i++)
            {
                var count = counts[i];
                var min = new Vector2(0.03f + i * 0.32f, 0.04f);
                var max = new Vector2(0.03f + i * 0.32f + 0.3f, 0.24f);
                var label = GachaConfirmPopup.CountLabel(count) + "\n<size=16>" + GachaUi.Money(GachaShop.CostOf(machine, count)) + "</size>";
                var accent = _shop.CanAfford(machine, count) ? Palette.NeonGreen : GachaUi.Muted;
                var button = GachaUi.MakeButton(card, "Pull" + count, label, min, max, accent, 22, () => _confirm.Show(_shop, machine, count));
                var text = GachaUi.LabelOf(button);
                if (text != null)
                {
                    text.lineSpacing = 0.85f;
                }
            }

            return card;
        }

        void BuildGauge(RectTransform card, GachaMachineData machine, int counter, bool boosting)
        {
            var legend = GachaService.LegendRate(machine, counter);
            var caption = boosting
                ? "다음 Legend " + GachaUi.Percent(legend) + " (상승 중)"
                : "다음 Legend " + GachaUi.Percent(legend) + " · " + machine.BoostStart + "회째부터 상승";
            GachaUi.Label(card, "GaugeLabel", 16, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.36f), new Vector2(0.75f, 0.45f),
                boosting ? GachaUi.LegendColor : GachaUi.Muted, caption);
            GachaUi.Label(card, "GaugeCount", 16, TextAnchor.MiddleRight, new Vector2(0.7f, 0.36f), new Vector2(0.96f, 0.45f), GachaUi.Muted,
                counter + "/" + machine.BoostCycle);

            var track = GachaUi.Panel(card, "GaugeTrack", new Vector2(0.04f, 0.285f), new Vector2(0.96f, 0.335f), new Color(0.1f, 0.12f, 0.2f, 1f), GachaUi.Muted);
            var cycle = Mathf.Max(1, machine.BoostCycle);
            var startMark = Mathf.Clamp01((machine.BoostStart - 1) / (float)cycle);
            var marker = GachaUi.Panel(track.transform, "StartMark", new Vector2(startMark, 0f), new Vector2(startMark, 1f), GachaUi.LegendColor).rectTransform;
            marker.sizeDelta = new Vector2(3f, 0f);
            var fill = Mathf.Clamp01(counter / (float)cycle);
            GachaUi.Panel(track.transform, "Fill", Vector2.zero, new Vector2(fill, 1f), boosting ? GachaUi.LegendColor : GachaUi.RareColor);
        }

        static void Tag(RectTransform parent, string name, string text, Color color, Vector2 min, Vector2 max)
        {
            var tag = GachaUi.Panel(parent, name, min, max, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 1f), color);
            GachaUi.Label(tag.transform, "Text", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, color, text);
        }

        void OpenInventory()
        {
            if (_flying || _confirm.IsOpen || _result.IsOpen || _refund.IsOpen)
            {
                return;
            }

            _inventory.Show(_shop);
        }

        void HandleInventoryRefund(int dollars)
        {
            RefreshCards();
        }

        void ConfirmPull(GachaMachineData machine, int count)
        {
            if (!_shop.TryPull(machine, count, out var result))
            {
                AudioManager.Ensure().PlayUpgradeDenied();
                if (_dollarPanel != null)
                {
                    Fx.Shake(_dollarPanel, 8f, 0.22f);
                }
                return;
            }

            AudioManager.Ensure().PlayUpgradeBuy();
            _confirm.Hide();
            RefreshCards();
            _result.Show(_shop, result);
        }

        void RequestRefund(List<RefundPick> picks, RectTransform from)
        {
            _pendingRefund = picks;
            var pieces = 0;
            var dollars = 0;
            foreach (var pick in picks)
            {
                pieces += pick.Count;
                dollars += _shop.RefundValue(pick.Gear.Id, pick.Count);
            }

            _refund.ShowConfirm(pieces, dollars, _shop.Balance);
        }

        // 확정 즉시 달러와 보관함을 저장하고, 그 뒤에 누른 버튼에서 잔액 패널로 달러가 날아간다.
        void ExecuteRefund(RectTransform from)
        {
            if (_pendingRefund == null || _flying)
            {
                return;
            }

            var machine = _result.Machine;
            var before = _shop.Balance;
            var total = 0;
            foreach (var pick in _pendingRefund)
            {
                var count = Mathf.Min(pick.Count, _shop.Inventory.Count(pick.Gear.Id, GearState.Unprocessed));
                if (count > 0 && _shop.TryRefund(pick.Gear.Id, GearState.Unprocessed, count, out var dollars))
                {
                    total += dollars;
                }
            }

            _pendingRefund = null;
            var after = _shop.Balance;
            _refund.LockConfirm();
            _result.Hide();
            RefreshCards();

            _flying = true;
            _flyBlocker.raycastTarget = true;
            _flyLayer.SetAsLastSibling();
            _flyFinish = () => _refund.ShowDone(machine, before, after);
            var duration = DollarFlyFx.Play(_flyLayer, from, _dollarPanel, total, _settings,
                null,
                FinishFly);
            _flyDeadline = Time.unscaledTime + duration + FlySafety;
            _refund.HideConfirmPanel();
        }

        void FinishFly()
        {
            if (!_flying)
            {
                return;
            }

            _flying = false;
            _flyBlocker.raycastTarget = false;
            var finish = _flyFinish;
            _flyFinish = null;
            finish?.Invoke();
        }

        void StorePicks(List<RefundPick> picks)
        {
            var stored = 0;
            foreach (var pick in picks)
            {
                var count = Mathf.Min(pick.Count, _shop.Inventory.Count(pick.Gear.Id, GearState.Unprocessed));
                if (count > 0 && _shop.Store(pick.Gear.Id, count))
                {
                    stored += count;
                }
            }

            _result.Hide();
            RefreshCards();
            ShowToast(stored > 0 ? "보관함에 " + stored + "개를 보관했어요" : "보관할 장비가 없어요");
        }

        // 결과창의 연속 뽑기: 결과를 닫고(고르지 않은 장비는 미처리로 남음) 같은 머신·횟수의 뽑기 확인을 띄운다.
        void PullAgain(GachaMachineData machine, int count)
        {
            if (machine == null || count <= 0)
            {
                return;
            }

            _result.Hide();
            RefreshCards();
            _confirm.Show(_shop, machine, count);
        }

        void CloseResult()
        {
            _result.Hide();
            RefreshCards();
            ShowToast("고르지 않은 장비는 보관함 미처리에 남아요");
        }

        void ShowToast(string message)
        {
            var panel = _toast.transform.parent as RectTransform;
            _toast.text = message;
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(true);
            var group = panel.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = panel.gameObject.AddComponent<CanvasGroup>();
            }

            group.blocksRaycasts = false;
            group.DOKill(false);
            group.alpha = 1f;
            Fx.Pop(panel);
            group.DOFade(0f, 0.3f).SetDelay(1.4f).SetUpdate(true).SetLink(panel.gameObject)
                .OnComplete(() => panel.gameObject.SetActive(false));
        }
    }
}
