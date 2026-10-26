using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 암시장 배너 목록(축제·데일리·상시·픽업). 타이틀·결과 화면 위에 덮어 띄우고, 닫으면 아래 화면으로 돌아간다.
    // 확인 팝업 → 결과 확정 → 머신 연출 영상(1연·10연, GachaVideoView) → 결과 목록 → 환원 확인·완료 또는 보관까지 이 화면이 흐름을 잇는다.
    // 화면 틀(BlackMarket 아래 패널·버튼·라벨)은 씬 하이러키에 두고 관리한다. 배너 카드·결과 목록만 실행 중에 만든다.
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
        GachaVideoView _video;
        GachaMachineData _lastMachine;
        readonly Dictionary<string, float> _refundRates = new Dictionary<string, float>();
        readonly List<BannerCard> _cards = new List<BannerCard>();
        GearSlot? _filter;
        List<RefundPick> _pendingRefund;
        bool _flying;
        float _flyDeadline;
        System.Action _flyFinish;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public GachaShop Shop => _shop;
        public GachaVideoView Video => _video;

        public void Build(RectTransform area)
        {
            _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            _shop = GachaShop.CreateDefault();
            foreach (var banner in _shop.Catalog.Banners)
            {
                _refundRates[banner.Id] = RefundRate(banner);
            }

            BuildLayout(area);
            _video = GachaVideoView.Create(_root);
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
            _refund = new GachaRefundPopup(_root, ExecuteRefund, ShowConfirm, RefreshCards);
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
            _video?.HideNow();
            SetDollarVisible(true);
            SelectTab(_filter);
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.DOKill(false);
            _group.DOFade(1f, _settings != null ? _settings.resultsFadeDuration : 0.25f).SetUpdate(true).SetLink(_root.gameObject);
        }

        public void Close()
        {
            if (!IsOpen || _flying || (_video != null && _video.IsPlaying))
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

            var modal = _confirm.IsOpen || _result.IsOpen || _refund.IsOpen || _inventory.IsOpen || _flying || (_video != null && _video.IsPlaying);
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
            PrepareSelected();
        }

        // 지금 고른 머신(탭의 머신, 전체 탭이면 마지막으로 고른 머신 또는 첫 머신)의 연출 영상을 미리 불러 둔다.
        void PrepareSelected()
        {
            if (_video == null)
            {
                return;
            }

            GachaMachineData pick = null;
            foreach (var machine in _shop.Catalog.Machines)
            {
                if (_filter.HasValue ? machine.Slot == _filter.Value : machine == _lastMachine)
                {
                    pick = machine;
                    break;
                }
            }

            if (pick == null && _shop.Catalog.Machines.Count > 0)
            {
                pick = _shop.Catalog.Machines[0];
            }

            _video.Prepare(pick);
        }

        void ShowConfirm(GachaBanner banner, int count)
        {
            if (banner.Machine != null)
            {
                _lastMachine = banner.Machine;
                _video?.Prepare(banner.Machine);
            }

            _confirm.Show(_shop, banner, count);
        }

        // 배너 카드 하나. 처음 한 번 만들고, 상점이 다시 보일 때마다 바뀌는 값(남은 횟수·비활성·게이지·데일리 머신)만 고친다.
        sealed class BannerCard
        {
            public GachaBanner Banner;
            public RectTransform Root;
            public Text Subtitle;
            public RectTransform Showcase;
            public string ShowcaseKey;
            public Text StateTag;
            public GameObject BoostTag;
            public GameObject NewTag;
            public Text GaugeLabel;
            public Text GaugeCount;
            public Image GaugeFill;
            public Text Status;
            public Button[] Buttons;
        }

        // 탭 전환·오리파 화면 복귀·결과 닫힘 때 부른다. 카드는 처음 한 번만 만들고, 이후에는 켜고 끄며 값만 갱신한다.
        // 데일리 완료 여부는 부를 때마다 KST 날짜를 다시 계산한다.
        void RefreshCards()
        {
            if (_list == null)
            {
                return;
            }

            if (_cards.Count == 0)
            {
                foreach (var banner in OrderedBanners())
                {
                    _cards.Add(BuildCard(banner));
                }
            }

            var index = 0;
            foreach (var card in _cards)
            {
                var visible = !_filter.HasValue || (card.Banner.Machine != null && card.Banner.Machine.Slot == _filter.Value);
                card.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                UpdateCard(card);
                var group = GachaUi.GetOrAdd<CanvasGroup>(card.Root);
                group.DOKill(false);
                group.alpha = 0f;
                group.DOFade(1f, 0.2f).SetDelay(Mathf.Min(index++, 8) * 0.06f).SetUpdate(true).SetLink(card.Root.gameObject);
            }
        }

        // 전체 탭 순서: 대축제 → 오늘의 암시장 → 상시 6종 → 픽업 18종. 머신 탭은 같은 순서에서 그 머신(상시 1 + 픽업)만 켠다.
        List<GachaBanner> OrderedBanners()
        {
            var list = new List<GachaBanner>(_shop.Catalog.Banners);
            list.Sort((a, b) =>
            {
                var byRank = Rank(a.Type).CompareTo(Rank(b.Type));
                return byRank != 0 ? byRank : a.Order.CompareTo(b.Order);
            });
            return list;
        }

        static int Rank(BannerType type)
        {
            switch (type)
            {
                case BannerType.Festival: return 0;
                case BannerType.Daily: return 1;
                case BannerType.Standard: return 2;
                default: return 3;
            }
        }

        BannerCard BuildCard(GachaBanner banner)
        {
            var card = new BannerCard { Banner = banner };
            var tint = GachaUi.BannerTint(banner);
            var cardImage = GachaUi.Panel(_list, "Banner_" + banner.Id, Vector2.zero, Vector2.one, GachaUi.Card, tint, true);
            var root = cardImage.rectTransform;
            card.Root = root;
            GachaUi.Height(root, 236f);
            if (banner.Type == BannerType.Pickup && banner.Pickup == PickupKind.Synchro)
            {
                CycleSynchro(cardImage.GetComponent<Outline>());
            }

            // 배너 전용 아트(bannerImage)가 있으면 그것을, 없으면 머신 배너, 그것도 없으면 색 바탕.
            var art = banner.BannerSprite != null ? banner.BannerSprite : banner.Machine != null ? banner.Machine.Banner : null;
            var bannerImage = GachaUi.Panel(root, "Banner", new Vector2(0.03f, 0.6f), new Vector2(0.97f, 0.96f),
                art != null ? Color.white : new Color(tint.r * 0.2f, tint.g * 0.2f, tint.b * 0.2f, 1f), tint);
            bannerImage.sprite = art;
            var name = GachaUi.Label(bannerImage.transform, "Name", 28, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.35f), new Vector2(0.7f, 0.95f), Palette.Text, banner.NameKo);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 28;
            card.Subtitle = GachaUi.Label(bannerImage.transform, "Slot", 16, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.05f), new Vector2(0.7f, 0.38f), tint);
            card.Subtitle.resizeTextForBestFit = true;
            card.Subtitle.resizeTextMinSize = 12;
            card.Subtitle.resizeTextMaxSize = 16;

            if (banner.Type == BannerType.Pickup)
            {
                Tag(bannerImage.rectTransform, "PickupTag", "PICK UP", tint, new Vector2(0.72f, 0.7f), new Vector2(0.97f, 0.95f));
                var icon = GachaUi.GearIcon(bannerImage.transform, "Featured", banner.Featured, new Vector2(0.75f, 0.04f), new Vector2(0.95f, 0.66f));
                var fit = icon.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1f;
            }
            else
            {
                card.Showcase = GachaUi.Rect(bannerImage.transform, "Showcase", new Vector2(0.62f, 0.04f), new Vector2(0.98f, 0.54f));
            }

            switch (banner.Type)
            {
                case BannerType.Festival:
                    card.StateTag = Tag(bannerImage.rectTransform, "FestivalTag", "FESTIVAL", tint, new Vector2(0.72f, 0.58f), new Vector2(0.97f, 0.92f));
                    break;
                case BannerType.Daily:
                    card.StateTag = Tag(bannerImage.rectTransform, "FreeTag", "FREE", tint, new Vector2(0.72f, 0.58f), new Vector2(0.97f, 0.92f));
                    break;
                case BannerType.Standard:
                    card.BoostTag = Tag(bannerImage.rectTransform, "BoostTag", "확률 상승 중", GachaUi.LegendColor, new Vector2(0.66f, 0.56f), new Vector2(0.97f, 0.92f))
                        .transform.parent.gameObject;
                    card.NewTag = Tag(bannerImage.rectTransform, "NewTag", "신규", Palette.NeonGreen, new Vector2(0.78f, 0.56f), new Vector2(0.97f, 0.92f))
                        .transform.parent.gameObject;
                    break;
            }

            GachaUi.Label(root, "Price", 22, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.46f), new Vector2(0.55f, 0.58f), Palette.Gold,
                banner.Price > 0 ? "1회 " + GachaUi.Money(banner.Price) : "무료 · 하루 1회");
            if (banner.Price > 0)
            {
                var rate = _refundRates.TryGetValue(banner.Id, out var cached) ? cached : RefundRate(banner);
                GachaUi.Label(root, "RefundRate", 16, TextAnchor.MiddleRight, new Vector2(0.45f, 0.46f), new Vector2(0.96f, 0.58f), Palette.Cyan,
                    "총 환원율 약 " + Mathf.RoundToInt(rate * 100f) + "%");
            }

            if (banner.Machine != null)
            {
                BuildGauge(card);
            }
            else
            {
                BuildStatus(card);
            }

            BuildPullButtons(card);
            return card;
        }

        void UpdateCard(BannerCard card)
        {
            var banner = card.Banner;
            card.Subtitle.text = Subtitle(banner);
            if (card.Showcase != null)
            {
                // 데일리는 요일마다 머신이 바뀌므로 오늘 머신이 달라졌을 때만 다시 그린다.
                var today = banner.Type == BannerType.Daily ? _shop.DailyMachine(banner) : null;
                var key = today != null ? today.Id : "all";
                if (card.ShowcaseKey != key)
                {
                    card.ShowcaseKey = key;
                    GachaUi.Showcase(card.Showcase, GachaUi.ShowcaseGears(_shop, banner));
                }
            }

            switch (banner.Type)
            {
                case BannerType.Festival:
                    card.StateTag.text = _shop.FestivalRemaining(banner) > 0 ? "FESTIVAL" : "완료";
                    break;
                case BannerType.Daily:
                    card.StateTag.text = _shop.DailyAvailable(banner) ? "FREE" : "완료";
                    break;
            }

            var counter = _shop.Counter(banner);
            var boosting = banner.Machine != null && counter + 1 >= banner.Machine.BoostStart;
            if (card.BoostTag != null)
            {
                card.BoostTag.SetActive(boosting);
                card.NewTag.SetActive(!boosting && _shop.Inventory.History.Count == 0);
            }

            if (banner.Machine != null)
            {
                UpdateGauge(card, counter, boosting);
            }
            else
            {
                UpdateStatus(card);
            }

            UpdatePullButtons(card);
        }

        string Subtitle(GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Daily:
                    return "오늘의 머신: " + GachaConfirmPopup.DailyMachineName(_shop, banner);
                case BannerType.Festival:
                    return "6종 머신 전체 · " + _shop.Catalog.AllGears.Count + "종";
                case BannerType.Pickup:
                    return banner.DescKo;
                default:
                    var machine = banner.Machine;
                    return (string.IsNullOrEmpty(machine.BannerCopy) ? GachaUi.SlotLabel(machine.Slot) : machine.BannerCopy) + " · " + machine.Gears.Count + "종";
            }
        }

        float RefundRate(GachaBanner banner)
        {
            return banner.Type == BannerType.Standard ? GachaService.ExpectedRefundRate(banner.Machine) : GachaService.ExpectedRefundRate(banner, _shop.Catalog);
        }

        // 축제·데일리: 게이지 자리에 설명과 남은 횟수(또는 갱신 안내).
        void BuildStatus(BannerCard card)
        {
            var desc = GachaUi.Label(card.Root, "Desc", 16, TextAnchor.UpperLeft, new Vector2(0.04f, 0.27f), new Vector2(0.68f, 0.45f), GachaUi.Muted, card.Banner.DescKo);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.resizeTextForBestFit = true;
            desc.resizeTextMinSize = 11;
            desc.resizeTextMaxSize = 16;
            card.Status = GachaUi.Label(card.Root, "Status", 16, TextAnchor.MiddleRight, new Vector2(0.68f, 0.27f), new Vector2(0.96f, 0.45f), Palette.Gold);
            card.Status.lineSpacing = 0.9f;
        }

        void UpdateStatus(BannerCard card)
        {
            var banner = card.Banner;
            if (banner.Type == BannerType.Festival)
            {
                var remaining = _shop.FestivalRemaining(banner);
                card.Status.text = remaining > 0 ? "남은 횟수\n" + remaining + "/" + banner.MaxPulls : "완료\n0/" + banner.MaxPulls;
                card.Status.color = remaining > 0 ? Palette.Gold : GachaUi.Muted;
            }
            else
            {
                var available = _shop.DailyAvailable(banner);
                card.Status.text = available ? "오늘 1회\n받을 수 있어요" : "내일 05:00\n갱신";
                card.Status.color = available ? Palette.NeonGreen : GachaUi.Muted;
            }
        }

        // 배너가 받는 횟수(1연·10연)만큼 버튼.
        void BuildPullButtons(BannerCard card)
        {
            var banner = card.Banner;
            var counts = banner.PullCounts;
            card.Buttons = new Button[counts.Length];
            const float gap = 0.02f;
            var width = (0.94f - gap * (counts.Length - 1)) / counts.Length;
            for (var i = 0; i < counts.Length; i++)
            {
                var count = counts[i];
                var min = new Vector2(0.03f + i * (width + gap), 0.04f);
                var max = new Vector2(min.x + width, 0.24f);
                var button = GachaUi.MakeButton(card.Root, "Pull" + count, string.Empty, min, max, Palette.NeonGreen, 22, () => ShowConfirm(banner, count));
                var text = GachaUi.LabelOf(button);
                if (text != null)
                {
                    text.lineSpacing = 0.85f;
                }

                card.Buttons[i] = button;
            }
        }

        // 횟수 제한·데일리 완료는 비활성화, 달러 부족은 눌러서 확인 팝업의 안내를 본다.
        void UpdatePullButtons(BannerCard card)
        {
            var banner = card.Banner;
            for (var i = 0; i < card.Buttons.Length; i++)
            {
                var count = banner.PullCounts[i];
                var cost = GachaShop.CostOf(banner, count);
                var block = _shop.Block(banner, count);
                var title = banner.Type == BannerType.Daily ? (block == BannerBlock.DailyDone ? "받기 완료" : "무료 1회") : GachaConfirmPopup.CountLabel(count);
                var text = GachaUi.LabelOf(card.Buttons[i]);
                if (text != null)
                {
                    text.text = cost > 0 ? title + "\n<size=16>" + GachaUi.Money(cost) + "</size>" : title;
                }

                var locked = block == BannerBlock.DailyDone || block == BannerBlock.LimitReached || block == BannerBlock.NotOffered;
                GachaUi.SetEnabled(card.Buttons[i], !locked, block == BannerBlock.None ? Palette.NeonGreen : GachaUi.Muted);
            }
        }

        // 싱크로 픽업 카드 테두리: 하양·핑크·연두를 번갈아.
        static void CycleSynchro(Outline outline)
        {
            if (outline == null)
            {
                return;
            }

            var colors = GachaVideoConfig.SynchroFlashColors;
            outline.effectColor = colors[0];
            var cycle = DOTween.Sequence().SetLoops(-1).SetUpdate(true).SetLink(outline.gameObject);
            for (var i = 0; i < colors.Length; i++)
            {
                var next = colors[(i + 1) % colors.Length];
                cycle.Append(DOTween.To(() => outline.effectColor, c => outline.effectColor = c, next, 0.4f).SetEase(Ease.InOutSine));
            }
        }

        // 상시·픽업이 같이 쓰는 머신 천장 게이지.
        void BuildGauge(BannerCard card)
        {
            var machine = card.Banner.Machine;
            card.GaugeLabel = GachaUi.Label(card.Root, "GaugeLabel", 16, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.36f), new Vector2(0.75f, 0.45f), GachaUi.Muted);
            card.GaugeCount = GachaUi.Label(card.Root, "GaugeCount", 16, TextAnchor.MiddleRight, new Vector2(0.7f, 0.36f), new Vector2(0.96f, 0.45f), GachaUi.Muted);
            var track = GachaUi.Panel(card.Root, "GaugeTrack", new Vector2(0.04f, 0.285f), new Vector2(0.96f, 0.335f), new Color(0.1f, 0.12f, 0.2f, 1f), GachaUi.Muted);
            var cycle = Mathf.Max(1, machine.BoostCycle);
            var startMark = Mathf.Clamp01((machine.BoostStart - 1) / (float)cycle);
            var marker = GachaUi.Panel(track.transform, "StartMark", new Vector2(startMark, 0f), new Vector2(startMark, 1f), GachaUi.LegendColor).rectTransform;
            marker.sizeDelta = new Vector2(3f, 0f);
            card.GaugeFill = GachaUi.Panel(track.transform, "Fill", Vector2.zero, Vector2.one, GachaUi.RareColor);
        }

        // 다음 Legend 확률은 배너 기준(싱크로 픽업은 기본 1.2%에서 오른다).
        static void UpdateGauge(BannerCard card, int counter, bool boosting)
        {
            var machine = card.Banner.Machine;
            var rates = new float[GachaService.RarityCount];
            GachaService.Rates(card.Banner, counter, true, rates);
            var legend = rates[(int)ItemRarity.Legend];
            card.GaugeLabel.text = boosting
                ? "다음 Legend " + GachaUi.Percent(legend) + " (상승 중)"
                : "다음 Legend " + GachaUi.Percent(legend) + " · " + machine.BoostStart + "회째부터 상승";
            card.GaugeLabel.color = boosting ? GachaUi.LegendColor : GachaUi.Muted;
            card.GaugeCount.text = counter + "/" + machine.BoostCycle;
            var fill = Mathf.Clamp01(counter / (float)Mathf.Max(1, machine.BoostCycle));
            card.GaugeFill.rectTransform.anchorMax = new Vector2(fill, 1f);
            card.GaugeFill.color = boosting ? GachaUi.LegendColor : GachaUi.RareColor;
        }

        static Text Tag(RectTransform parent, string name, string text, Color color, Vector2 min, Vector2 max)
        {
            var tag = GachaUi.Panel(parent, name, min, max, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 1f), color);
            return GachaUi.Label(tag.transform, "Text", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, color, text);
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

        void ConfirmPull(GachaBanner banner, int count)
        {
            if (!_shop.TryPull(banner, count, out var result))
            {
                AudioManager.Ensure().PlayUpgradeDenied();
                if (_dollarPanel != null && _shop.Block(banner, count) == BannerBlock.NoDollars)
                {
                    Fx.Shake(_dollarPanel, 8f, 0.22f);
                }
                return;
            }

            AudioManager.Ensure().PlayUpgradeBuy();
            _confirm.Hide();
            RefreshCards();
            // 결과는 이미 확정·저장됐다. 이 탭과 같은 프레임에 영상과 소리를 시작하고, 끝나면(또는 못 불러오면) 결과 목록을 띄운다.
            // 연출 중(영상 시작 ~ 결과 카드를 닫을 때)에는 달러 패널을 숨긴다.
            SetDollarVisible(false);
            if (_video == null || !_video.Play(result, () => FinishVideo(result)))
            {
                FinishVideo(result);
            }
        }

        void FinishVideo(GachaPullResult result)
        {
            SetDollarVisible(true);
            _result.Show(_shop, result);
        }

        // 달러 패널은 맨 앞에 유지되므로(UiStayOnTop) 끄지 않고 CanvasGroup으로만 감춘다.
        void SetDollarVisible(bool visible)
        {
            if (_dollarPanel == null)
            {
                return;
            }

            var group = GachaUi.GetOrAdd<CanvasGroup>(_dollarPanel);
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
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

            var banner = _result.Banner;
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
            _flyFinish = () => _refund.ShowDone(banner, before, after);
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

        // 결과창의 연속 뽑기: 결과를 닫고(고르지 않은 장비는 미처리로 남음) 같은 배너·횟수의 뽑기 확인을 띄운다.
        void PullAgain(GachaBanner banner, int count)
        {
            if (banner == null || count <= 0)
            {
                return;
            }

            _result.Hide();
            RefreshCards();
            ShowConfirm(banner, count);
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
