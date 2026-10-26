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
        // 아트 카드: 화면에 보이는 카드만 그림을 불러오고 애니메이션을 돌린다(이 간격으로 확인).
        const float VisibilityInterval = 0.1f;
        const float LegacyCardHeight = 236f;

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
        float _nextVisibilityCheck;
        System.Action _flyFinish;
        System.Action _onSettings;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public GachaShop Shop => _shop;
        public ScrollRect Scroll => _scroll;
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

            var notice = GachaUi.Label(_root, "Notice", 16, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.89f), new Vector2(0.97f, 0.922f), GachaUi.Muted, Notice);
            OripaLook.Behind(notice, "strip_info");
            BuildTabs();
            _list = GachaUi.Scroll(_root, "Machines", new Vector2(0.03f, 0.015f), new Vector2(0.97f, 0.828f), 10f, out _scroll);

            _result = new GachaResultView(_root, RequestRefund, StorePicks, CloseResult);
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
            SkinDollarPanel(true);
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
                .OnComplete(() =>
                {
                    _root.gameObject.SetActive(false);
                    SkinDollarPanel(false);
                    foreach (var card in _cards)
                    {
                        SetPlaying(card, false);
                    }
                });
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

            if (Time.unscaledTime >= _nextVisibilityCheck)
            {
                _nextVisibilityCheck = Time.unscaledTime + VisibilityInterval;
                UpdateVisibleCards();
            }
        }

        void BuildTopBar()
        {
            var bar = GachaUi.Panel(_root, "TopBar", new Vector2(0f, 0.925f), Vector2.one, new Color(0.02f, 0.03f, 0.07f, 1f), Palette.Magenta, true).rectTransform;
            // 로고와 달러 패널은 없앤다. 달러는 타이틀 화면의 달러 패널이 오리파 위에 그대로 보인다.
            HideLegacy(bar, "Logo");
            HideLegacy(bar, "DollarPanel");
            // 오른쪽 위 끝은 설정 톱니(타이틀·HUD와 같은 버튼), 보관함·닫기는 그 왼쪽으로. 씬에 있던 버튼도 이 자리로 옮긴다.
            var inventory = GachaUi.MakeButton(bar, "Inventory", "보관함", InventoryMin, InventoryMax, Palette.NeonGreen, 22, OpenInventory);
            GachaUi.Stretch((RectTransform)inventory.transform, InventoryMin, InventoryMax);
            _close = GachaUi.MakeButton(bar, "Close", "X", CloseMin, CloseMax, Palette.Magenta, 28, Close);
            GachaUi.Stretch((RectTransform)_close.transform, CloseMin, CloseMax);
            // 스킨: 보관함(초록)·닫기(분홍, 그림에 X가 있어 글자는 뺀다).
            OripaLook.Button(inventory, "btn_storage", Palette.NeonGreen);
            if (OripaLook.Button(_close, "btn_close_x"))
            {
                GachaUi.LabelOf(_close).text = string.Empty;
                ((Image)_close.targetGraphic).preserveAspect = true;
            }
            if (bar.Find("SettingsButton") == null)
            {
                SettingsView.CreateGearButton(bar, SettingsMin, SettingsMax, () => _onSettings?.Invoke());
            }
        }

        static readonly Vector2 InventoryMin = new Vector2(0.55f, 0.14f);
        static readonly Vector2 InventoryMax = new Vector2(0.73f, 0.86f);
        static readonly Vector2 CloseMin = new Vector2(0.75f, 0.14f);
        static readonly Vector2 CloseMax = new Vector2(0.86f, 0.86f);
        static readonly Vector2 SettingsMin = new Vector2(0.88f, 0.08f);
        static readonly Vector2 SettingsMax = new Vector2(0.99f, 0.92f);

        public void BindSettings(System.Action onSettings)
        {
            _onSettings = onSettings;
        }

        public void SetDollarTarget(RectTransform panel)
        {
            _dollarPanel = panel;
        }

        // 오리파가 열려 있는 동안만 달러 패널을 오리파 스킨(topbar_dollar, 그림에 $가 있어 $ 아이콘은 숨김)으로 바꾸고, 닫으면 되돌린다.
        Sprite _dollarSprite;
        Image.Type _dollarType;
        Color _dollarColor;
        bool _dollarSkinned;

        void SkinDollarPanel(bool on)
        {
            var image = _dollarPanel != null ? _dollarPanel.GetComponent<Image>() : null;
            var icon = _dollarPanel != null ? _dollarPanel.Find("DollarIcon") : null;
            if (image == null || on == _dollarSkinned || (on && OripaLook.Get("topbar_dollar") == null))
            {
                return;
            }

            if (on)
            {
                _dollarSprite = image.sprite;
                _dollarType = image.type;
                _dollarColor = image.color;
                OripaLook.Apply(image, "topbar_dollar");
            }
            else
            {
                var fit = image.GetComponent<SlicedFit>();
                if (fit != null)
                {
                    Destroy(fit);
                }

                image.sprite = _dollarSprite;
                image.type = _dollarType;
                image.color = _dollarColor;
                image.pixelsPerUnitMultiplier = 1f;
            }

            if (icon != null)
            {
                icon.gameObject.SetActive(!on);
            }

            _dollarSkinned = on;
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

                GachaUi.StyleTab(_tabButtons[i], active);
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

            // 아트 카드(bannerImage가 있는 배너): 그림은 처음 보일 때 불러오고, 효과는 보일 때만 돈다.
            public bool Art;
            public Image ArtImage;
            public bool ArtLoaded;
            public EllipsisText Desc;
            public Text Info;
            public readonly List<Tween> Effects = new List<Tween>();
            public bool Playing;
            // 스킨 테두리(속이 빈 banner_frame_*, 그림 위에 얹음)와 완료 표시용 어둡게 덮는 판.
            public Image Frame;
            public Image Dim;
        }

        public IEnumerable<(GachaBanner Banner, RectTransform Root, Image Art, bool Loaded, bool Playing, EllipsisText Desc, Button[] Buttons)> CardStates()
        {
            foreach (var card in _cards)
            {
                yield return (card.Banner, card.Root, card.ArtImage, card.ArtLoaded, card.Playing, card.Desc, card.Buttons);
            }
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

            SortCards();
            var index = 0;
            var artHeight = ArtCardHeight();
            foreach (var card in _cards)
            {
                var visible = !_filter.HasValue || (card.Banner.Machine != null && card.Banner.Machine.Slot == _filter.Value);
                card.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    SetPlaying(card, false);
                    continue;
                }

                if (card.Art)
                {
                    GachaUi.Height(card.Root, artHeight);
                }

                UpdateCard(card);
                var group = GachaUi.GetOrAdd<CanvasGroup>(card.Root);
                group.DOKill(false);
                group.alpha = 0f;
                group.DOFade(1f, 0.2f).SetDelay(Mathf.Min(index++, 8) * 0.06f).SetUpdate(true).SetLink(card.Root.gameObject);
            }

            _nextVisibilityCheck = 0f;
        }

        // 아트 카드 높이 = 카드 폭 × 0.6(4:3 그림의 아래 어두운 띠를 자른 높이). 카드 폭은 목록 폭에서 좌우 여백을 뺀 값.
        float ArtCardHeight()
        {
            var layout = _list.GetComponent<VerticalLayoutGroup>();
            var padding = layout != null ? layout.padding.horizontal : 0;
            var width = _list.rect.width - padding;
            if (width <= 1f)
            {
                width = 480f * 0.94f - padding;
            }

            return width * BannerArt.CardAspect;
        }

        // 스크롤 영역에 걸친 카드만 그림을 불러오고(처음 한 번) 애니메이션을 돌린다. 나머지는 멈춘다.
        void UpdateVisibleCards()
        {
            if (_scroll == null || _cards.Count == 0)
            {
                return;
            }

            var viewport = WorldRect(_scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform);
            foreach (var card in _cards)
            {
                var visible = card.Root.gameObject.activeInHierarchy && WorldRect(card.Root).Overlaps(viewport);
                if (visible && card.Art && !card.ArtLoaded)
                {
                    LoadArt(card);
                }

                SetPlaying(card, visible);
            }
        }

        static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static void LoadArt(BannerCard card)
        {
            card.ArtLoaded = true;
            var sprite = card.Banner.BannerSprite;
            if (sprite == null)
            {
                Debug.LogWarning("배너 그림을 불러오지 못했습니다: Resources/" + card.Banner.BannerImage + " (" + card.Banner.Id + ")");
                return;
            }

            card.ArtImage.sprite = sprite;
            card.ArtImage.color = Color.white;
        }

        static void SetPlaying(BannerCard card, bool playing)
        {
            if (card.Playing == playing)
            {
                return;
            }

            card.Playing = playing;
            foreach (var tween in card.Effects)
            {
                if (tween == null || !tween.IsActive())
                {
                    continue;
                }

                if (playing)
                {
                    tween.Play();
                }
                else
                {
                    tween.Pause();
                }
            }
        }

        // 순서: 대축제 → 오늘의 암시장 → 싱크로 픽업 → 레전드 픽업 → 상시. 같은 묶음은 json order(머신 순) 그대로.
        // 머신 탭은 같은 순서에서 그 머신만 켠다(싱크로 → 레전드 2종 → 상시). 받기 완료한 오늘의 암시장은 맨 아래로.
        List<GachaBanner> OrderedBanners()
        {
            var list = new List<GachaBanner>(_shop.Catalog.Banners);
            list.Sort(CompareBanners);
            return list;
        }

        int CompareBanners(GachaBanner a, GachaBanner b)
        {
            var byRank = Rank(a).CompareTo(Rank(b));
            return byRank != 0 ? byRank : a.Order.CompareTo(b.Order);
        }

        int Rank(GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Festival: return 0;
                case BannerType.Daily: return _shop.DailyAvailable(banner) ? 1 : 9;
                case BannerType.Pickup: return banner.Pickup == PickupKind.Synchro ? 2 : 3;
                default: return 4;
            }
        }

        // 데일리 완료 여부가 바뀌면 순서도 바뀌므로 갱신할 때마다 카드 순서를 다시 맞춘다.
        void SortCards()
        {
            _cards.Sort((a, b) => CompareBanners(a.Banner, b.Banner));
            for (var i = 0; i < _cards.Count; i++)
            {
                _cards[i].Root.SetSiblingIndex(i);
            }
        }

        BannerCard BuildCard(GachaBanner banner)
        {
            // 배너 그림 경로가 있으면 그림 카드, 없으면 예전 카드 디자인.
            if (!string.IsNullOrEmpty(banner.BannerImage))
            {
                return BuildArtCard(banner);
            }

            var card = new BannerCard { Banner = banner };
            var tint = GachaUi.BannerTint(banner);
            var cardImage = GachaUi.Panel(_list, "Banner_" + banner.Id, Vector2.zero, Vector2.one, GachaUi.Card, tint, true);
            var root = cardImage.rectTransform;
            card.Root = root;
            GachaUi.Height(root, LegacyCardHeight);
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

        // 그림 카드(폭 × 0.6): 4:3 그림을 카드 위에 맞춰 붙이고, 카드 밖으로 나가는 아래 어두운 띠만 RectMask2D로 자른다(그림 파일은 그대로).
        // 왼쪽에 태그·제목·설명(2줄)·가격/상태·천장 게이지, 오른쪽 아래에 1연·10연 버튼. 글자·버튼은 예전(폭 × 0.75) 카드와 같은 픽셀 크기다.
        // 픽업 장비 아이콘·대축제 쇼케이스는 그림에 주인공이 있어 쓰지 않는다.
        const float ArtButtonWidth = 0.23f;
        const float ArtButtonGap = 0.02f;
        const float ArtButtonRight = 0.97f;
        // 예전 카드에서 버튼은 y 0.03~0.2. 같은 픽셀 크기·아래 여백으로 새 카드에 옮긴다.
        const float ArtButtonBottom = 0.03f * BannerArt.Rescale;
        const float ArtButtonTop = ArtButtonBottom + 0.17f * BannerArt.Rescale;
        // 왼쪽 글자 영역: 예전과 같은 픽셀 높이(예전 카드의 0.67)를 카드 위쪽에 붙인다.
        const float ArtTextTop = 0.95f;
        const float ArtTextBottom = ArtTextTop - 0.67f * BannerArt.Rescale;
        // 가격 줄·천장 게이지 오른쪽 끝은 버튼 왼쪽 끝보다 이만큼(px) 앞.
        const float ArtButtonClearance = 8f;

        static float ArtButtonsLeft(int count)
        {
            return ArtButtonRight - count * ArtButtonWidth - (count - 1) * ArtButtonGap;
        }

        // 글자 영역(예전 0~1 비율)의 y를 카드 기준으로.
        static float ArtTextY(float local)
        {
            return ArtTextBottom + local * (ArtTextTop - ArtTextBottom);
        }

        // "Text" 칸은 글자 영역의 0.21~1(태그·제목·설명)만 덮는다. 그 안의 비율로 바꾼다.
        static float Local(float local)
        {
            return (local - 0.21f) / (1f - 0.21f);
        }

        BannerCard BuildArtCard(GachaBanner banner)
        {
            var card = new BannerCard { Banner = banner, Art = true };
            var tint = GachaUi.BannerTint(banner);
            var frame = GachaUi.Panel(_list, "Banner_" + banner.Id, Vector2.zero, Vector2.one, new Color(tint.r * 0.15f, tint.g * 0.15f, tint.b * 0.15f, 1f), null, true);
            card.Root = frame.rectTransform;
            GachaUi.GetOrAdd<RectMask2D>(card.Root);
            GachaUi.Height(card.Root, ArtCardHeight());
            // 그림: 폭 전체, 높이 = 폭 × 0.75(카드 높이의 1.25배), 위 끝을 카드 위 끝에 맞춘다.
            var image = GachaUi.Panel(card.Root, "Art", new Vector2(0f, BannerArt.ArtBottom), Vector2.one, new Color(1f, 1f, 1f, 0f));
            image.preserveAspect = true;
            card.ArtImage = image;
            // 받기 완료·소진 때 그림 위에 깔 검정(알파 0.35).
            card.Dim = GachaUi.Panel(card.Root, "DoneDim", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.35f));
            card.Dim.gameObject.SetActive(false);
            BannerArt.AddFades(card.Root, banner);
            // 테두리: 스킨이 있으면 배너 종류별 그림 테두리를 그림 위에 얹고, 없으면 카드 틀에 선 테두리(마스크 바깥이라 잘리지 않는다).
            card.Frame = OripaLook.Layer(card.Root, "Frame", FrameSprite(banner), Vector2.zero, Vector2.one, null, false);
            if (card.Frame == null)
            {
                BannerArt.Border(frame, banner, card.Effects);
            }
            else if (banner.Type == BannerType.Pickup && banner.Pickup == PickupKind.Synchro)
            {
                // 싱크로: 크리스탈 테두리 그림 위에 밝기만 은은하게 맥동.
                var crystal = card.Frame;
                card.Effects.Add(DOTween.To(() => crystal.color, c => crystal.color = c, new Color(0.72f, 0.72f, 0.72f, 1f), 1.1f)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(crystal.gameObject));
            }

            var text = GachaUi.Rect(card.Root, "Text", new Vector2(0.05f, ArtTextY(0.21f)), new Vector2(0.55f, ArtTextTop));
            var tagMin = new Vector2(0f, Local(0.86f));
            var tagMax = new Vector2(0.46f, 1f);
            switch (banner.Type)
            {
                case BannerType.Pickup:
                    Tag(text, "PickupTag", "PICK UP", tint, tagMin, tagMax);
                    break;
                case BannerType.Festival:
                    card.StateTag = Tag(text, "FestivalTag", "FESTIVAL", tint, tagMin, tagMax);
                    break;
                case BannerType.Daily:
                    card.StateTag = Tag(text, "FreeTag", "FREE", tint, tagMin, tagMax);
                    break;
                case BannerType.Standard:
                    card.BoostTag = Tag(text, "BoostTag", "확률 상승 중", GachaUi.LegendColor, tagMin, new Vector2(0.6f, 1f)).transform.parent.gameObject;
                    card.NewTag = Tag(text, "NewTag", "신규", Palette.NeonGreen, tagMin, new Vector2(0.3f, 1f)).transform.parent.gameObject;
                    break;
            }

            var title = GachaUi.Label(text, "Name", 28, TextAnchor.MiddleLeft, new Vector2(0f, Local(0.5f)), new Vector2(1f, Local(0.84f)), Palette.Text, BannerArt.Title(banner));
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 28;
            title.lineSpacing = 0.9f;
            BannerArt.StyleTitle(title, banner, card.Effects);

            var desc = GachaUi.Label(text, "Desc", 14, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, Local(0.48f)), new Color(0.88f, 0.92f, 1f, 0.95f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
            desc.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            card.Desc = desc.gameObject.AddComponent<EllipsisText>();
            card.Desc.Set(DescriptionOf(banner), 2);

            // 가격·상태 줄과 천장 게이지는 오른쪽 아래 버튼과 같은 높이라, 오른쪽 끝을 버튼 왼쪽 끝 8px 앞에서 끊는다.
            var clearRight = ArtButtonsLeft(banner.PullCounts.Length);
            card.Info = GachaUi.Label(card.Root, "Info", 14, TextAnchor.MiddleLeft, new Vector2(0.05f, ArtTextY(0.07f)), new Vector2(clearRight, ArtTextY(0.2f)), Palette.Gold);
            card.Info.rectTransform.offsetMax = new Vector2(-ArtButtonClearance, 0f);
            card.Info.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.Info.resizeTextForBestFit = true;
            card.Info.resizeTextMinSize = 11;
            card.Info.resizeTextMaxSize = 14;
            card.Info.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            if (banner.Machine != null)
            {
                // 천장 게이지(얇은 선). 상태 글자 바로 아래. 두께는 예전 카드와 같은 픽셀.
                var track = GachaUi.Panel(card.Root, "GaugeTrack", new Vector2(0.05f, ArtTextY(0f)), new Vector2(clearRight, ArtTextY(0.035f)), new Color(0.1f, 0.12f, 0.2f, 0.9f));
                track.rectTransform.offsetMax = new Vector2(-ArtButtonClearance, 0f);
                card.GaugeFill = GachaUi.Panel(track.transform, "Fill", Vector2.zero, Vector2.one, GachaUi.RareColor);
            }

            BuildPullButtons(card);
            foreach (var tween in card.Effects)
            {
                tween.Pause();
            }

            return card;
        }

        public static string FrameSprite(GachaBanner banner)
        {
            switch (banner.Type)
            {
                case BannerType.Festival: return "banner_frame_festival";
                case BannerType.Daily: return "banner_frame_daily";
                case BannerType.Pickup: return banner.Pickup == PickupKind.Synchro ? "banner_frame_synchro" : "banner_frame_legend";
                default: return "banner_frame_standard";
            }
        }

        // 카드 설명(2줄까지, 넘치면 …). 잘린 전체 문구는 확인 팝업에서 보여 준다.
        public static string DescriptionOf(GachaBanner banner)
        {
            if (!string.IsNullOrEmpty(banner.DescKo))
            {
                return banner.DescKo;
            }

            return banner.Machine != null ? banner.Machine.BannerCopy : string.Empty;
        }

        // 아트 카드의 가격·상태 한 줄과 천장 게이지.
        void UpdateArtInfo(BannerCard card, int counter, bool boosting)
        {
            var banner = card.Banner;
            var price = banner.Price > 0 ? "1회 " + GachaUi.Money(banner.Price) : "무료";
            switch (banner.Type)
            {
                case BannerType.Festival:
                    var remaining = _shop.FestivalRemaining(banner);
                    card.Info.text = remaining > 0
                        ? price + (banner.DailyLimit ? " · 오늘 남은 " : " · 남은 횟수 ") + remaining + "/" + banner.MaxPulls
                        : banner.DailyLimit ? "오늘 완료 · 내일 05:00 갱신" : "완료 · 0/" + banner.MaxPulls;
                    card.Info.color = remaining > 0 ? Palette.Gold : GachaUi.Muted;
                    break;
                case BannerType.Daily:
                    var available = _shop.DailyAvailable(banner);
                    card.Info.text = available
                        ? price + " · 오늘 " + GachaConfirmPopup.DailyMachineName(_shop, banner)
                        : "오늘 완료 · 내일 05:00 갱신";
                    card.Info.color = available ? Palette.NeonGreen : GachaUi.Muted;
                    break;
                default:
                    var machine = banner.Machine;
                    card.Info.text = price + " · 천장 " + counter + "/" + machine.BoostCycle + (boosting ? " 상승 중" : string.Empty);
                    card.Info.color = boosting ? GachaUi.LegendColor : Palette.Gold;
                    var fill = Mathf.Clamp01(counter / (float)Mathf.Max(1, machine.BoostCycle));
                    card.GaugeFill.rectTransform.anchorMax = new Vector2(fill, 1f);
                    card.GaugeFill.color = boosting ? GachaUi.LegendColor : GachaUi.RareColor;
                    break;
            }

            // "1회 $9,000 · 오늘 남은 30/30"이 두 줄로 쪼개지지 않게 한 줄로 두고 넘치면 줄인다.
            OripaLook.FitOneLine(card.Info, 14, 10);
        }

        void UpdateCard(BannerCard card)
        {
            var banner = card.Banner;
            if (card.Subtitle != null)
            {
                card.Subtitle.text = Subtitle(banner);
            }
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

            var done = (banner.Type == BannerType.Festival && _shop.FestivalRemaining(banner) <= 0)
                || (banner.Type == BannerType.Daily && !_shop.DailyAvailable(banner));
            switch (banner.Type)
            {
                case BannerType.Festival:
                    card.StateTag.text = done ? OripaLook.CheckGlyph(card.StateTag) + "완료" : "FESTIVAL";
                    break;
                case BannerType.Daily:
                    card.StateTag.text = done ? OripaLook.CheckGlyph(card.StateTag) + "완료" : "FREE";
                    break;
            }

            // 받기 완료·축제 소진: 테두리 밝기 40%, 그림 위 검정 0.35(버튼 비활성은 아래 UpdatePullButtons).
            if (card.Dim != null)
            {
                card.Dim.gameObject.SetActive(done);
            }

            if (card.Frame != null && !(banner.Type == BannerType.Pickup && banner.Pickup == PickupKind.Synchro))
            {
                card.Frame.color = done ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
            }

            var counter = _shop.Counter(banner);
            var boosting = banner.Machine != null && counter + 1 >= banner.Machine.BoostStart;
            if (card.BoostTag != null)
            {
                card.BoostTag.SetActive(boosting);
                card.NewTag.SetActive(!boosting && _shop.Inventory.History.Count == 0);
            }

            if (card.Art)
            {
                UpdateArtInfo(card, counter, boosting);
            }
            else if (banner.Machine != null)
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
            // 아트 카드는 아래 띠에 오른쪽 정렬로 나란히(주인공 그림을 가리지 않게 작게), 예전 카드는 폭 전체.
            var width = card.Art ? ArtButtonWidth : (0.94f - gap * (counts.Length - 1)) / counts.Length;
            var left = card.Art ? ArtButtonsLeft(counts.Length) : 0.03f;
            for (var i = 0; i < counts.Length; i++)
            {
                var count = counts[i];
                var min = new Vector2(left + i * (width + (card.Art ? ArtButtonGap : gap)), card.Art ? ArtButtonBottom : 0.04f);
                var max = new Vector2(min.x + width, card.Art ? ArtButtonTop : 0.24f);
                var button = GachaUi.MakeButton(card.Root, "Pull" + count, string.Empty, min, max, Palette.NeonGreen, card.Art ? 16 : 22, () => ShowConfirm(banner, count));
                OripaLook.Button(button, PullSprite(count));
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
                    var small = card.Art ? 12 : 16;
                    var discount = GachaShop.DiscountPercent(banner, count);
                    var off = discount > 0 ? " <color=" + GachaUi.Hex(Palette.Gold) + ">-" + discount + "%</color>" : string.Empty;
                    text.text = cost > 0 ? title + off + "\n<size=" + small + ">" + GachaUi.Money(cost) + "</size>" : title;
                }

                var locked = block == BannerBlock.DailyDone || block == BannerBlock.LimitReached || block == BannerBlock.NotOffered;
                GachaUi.SetEnabled(card.Buttons[i], !locked, block == BannerBlock.None ? Palette.NeonGreen : GachaUi.Muted);
                // 스킨: 1연 btn_pull, 10연 btn_pull_ten(강조), 받기 완료·소진·달러 부족은 btn_pull_disabled(달러 부족은 눌러서 안내를 볼 수 있다).
                OripaLook.SwapState(card.Buttons[i], block == BannerBlock.None, PullSprite(count), "btn_pull_disabled", count > 1 ? Palette.Cyan : Palette.NeonGreen);
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

        static string PullSprite(int count)
        {
            return count > 1 ? "btn_pull_ten" : "btn_pull";
        }

        // 태그: 스킨이면 FESTIVAL은 tag_plate_pink, 나머지(PICK UP·FREE·완료·확률 상승 중·신규)는 tag_plate_white를 색으로 틴트.
        static Text Tag(RectTransform parent, string name, string text, Color color, Vector2 min, Vector2 max)
        {
            var tag = GachaUi.Panel(parent, name, min, max, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 1f), color);
            if (name == "FestivalTag")
            {
                OripaLook.Apply(tag, "tag_plate_pink");
            }
            else
            {
                OripaLook.Apply(tag, "tag_plate_white", true, color);
            }

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
