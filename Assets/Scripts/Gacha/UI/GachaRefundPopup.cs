using System;
using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.FX;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 환원 확인(선택 개수, 달러 전→후, 확정/취소)과 환원 완료(체크, 전→후, 같은 배너 다시 뽑기).
    public sealed class GachaRefundPopup
    {
        readonly RectTransform _root;
        readonly CanvasGroup _group;
        readonly RectTransform _confirmPanel;
        readonly Text _confirmCount;
        readonly Text _confirmBalance;
        readonly Button _confirmButton;
        readonly RectTransform _donePanel;
        readonly RectTransform _check;
        readonly Text _doneBalance;
        readonly Text _retryName;
        readonly Text _retryPrice;
        readonly Action<RectTransform> _onConfirm;
        readonly Action<GachaBanner, int> _onRetry;
        readonly Action _onClose;
        readonly Button _retryTen;
        GachaBanner _banner;
        int _pieces;
        // 스킨: 잔액 value_box → arrow_right → value_box_gain.
        readonly Text _before;
        readonly Text _after;

        public bool IsOpen => _root.gameObject.activeSelf;

        public GachaRefundPopup(RectTransform parent, Action<RectTransform> onConfirm, Action<GachaBanner, int> onRetry, Action onClose)
        {
            _onConfirm = onConfirm;
            _onRetry = onRetry;
            _onClose = onClose;
            _root = GachaUi.Panel(parent, "GachaRefund", Vector2.zero, new Vector2(1f, 0.925f), GachaUi.Dim, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            _confirmPanel = GachaUi.Panel(_root, "ConfirmPanel", new Vector2(0.07f, 0.3f), new Vector2(0.93f, 0.7f), GachaUi.Card, Palette.Gold, true).rectTransform;
            LocText.Bind(GachaUi.Label(_confirmPanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.8f), new Vector2(0.96f, 0.96f), Palette.Gold),
                "ui.result.refund");
            _confirmCount = GachaUi.Label(_confirmPanel, "Count", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.64f), new Vector2(0.96f, 0.78f), Palette.Text);
            _confirmBalance = GachaUi.Label(_confirmPanel, "Balance", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.45f), new Vector2(0.96f, 0.62f), Palette.Text);
            var note = GachaUi.Label(_confirmPanel, "Note", 16, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.33f), new Vector2(0.96f, 0.44f), GachaUi.Muted);
            LocText.Bind(note, "ui.refund.irreversible");
            var cancel = LocText.Bind(GachaUi.MakeButton(_confirmPanel, "Cancel", string.Empty, new Vector2(0.05f, 0.06f), new Vector2(0.45f, 0.28f), GachaUi.Muted, 22,
                HideConfirm), "ui.common.cancel");
            _confirmButton = GachaUi.MakeButton(_confirmPanel, "Confirm", string.Empty, new Vector2(0.5f, 0.06f), new Vector2(0.95f, 0.28f), Palette.NeonGreen, 22,
                () => _onConfirm?.Invoke((RectTransform)_confirmButton.transform));
            LocText.Bind(_confirmButton, "ui.refund.confirm");

            // 스킨: popup_gold(제목 바 포함, 제목은 노란 바 위 어두운 글자), 잔액 칸, "되돌릴 수 없습니다"는 warning_strip(왼쪽 ! 아이콘) 안, 버튼 btn_gray / btn_green.
            if (OripaLook.Apply(_confirmPanel.GetComponent<Image>(), "popup_gold"))
            {
                var title = (RectTransform)_confirmPanel.Find("Title");
                GachaUi.Stretch(title, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.95f));
                title.GetComponent<Text>().color = Palette.Gold;
                GachaUi.Stretch(note.rectTransform, new Vector2(0.06f, 0.32f), new Vector2(0.94f, 0.43f));
                OripaLook.Behind(note, "warning_strip");
                note.rectTransform.offsetMin = new Vector2(34f, 0f);
                note.color = Palette.Gold;
                TextOneLine.Fit(note, 16, 11);

                _confirmBalance.gameObject.SetActive(false);
                var row = GachaUi.Rect(_confirmPanel, "BalanceRow", new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.61f));
                var beforeBox = OripaLook.Layer(row, "Before", "value_box", Vector2.zero, new Vector2(0.42f, 1f));
                _before = GachaUi.Label(beforeBox.transform, "Text", 22, TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), Palette.Text);
                OripaLook.Layer(row, "Arrow", "arrow_right", new Vector2(0.44f, 0.1f), new Vector2(0.56f, 0.9f));
                var afterBox = OripaLook.Layer(row, "After", "value_box_gain", new Vector2(0.58f, 0f), Vector2.one);
                _after = GachaUi.Label(afterBox.transform, "Text", 22, TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), Palette.NeonGreen);
                OripaLook.Button(cancel, "btn_gray", Palette.Text);
                OripaLook.Button(_confirmButton, "btn_green", Palette.Text);
            }

            _donePanel = GachaUi.Panel(_root, "DonePanel", new Vector2(0.07f, 0.22f), new Vector2(0.93f, 0.78f), GachaUi.Card, Palette.NeonGreen, true).rectTransform;
            _check = GachaUi.CheckMark(_donePanel, "Check", new Vector2(0.4f, 0.79f), new Vector2(0.6f, 0.95f), Palette.NeonGreen);
            LocText.Bind(GachaUi.Label(_donePanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.78f), Palette.NeonGreen),
                "ui.refund.done");
            _doneBalance = GachaUi.Label(_donePanel, "Balance", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.56f), new Vector2(0.96f, 0.67f), Palette.Text);

            var retry = GachaUi.Panel(_donePanel, "Retry", new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.52f), GachaUi.Inset, Palette.Magenta).rectTransform;
            LocText.Bind(GachaUi.Label(retry, "Caption", 16, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.96f), GachaUi.Muted),
                "ui.refund.pull_again");
            _retryName = GachaUi.Label(retry, "Name", 22, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.48f), new Vector2(0.6f, 0.76f), Palette.Text);
            _retryPrice = GachaUi.Label(retry, "Price", 16, TextAnchor.MiddleRight, new Vector2(0.55f, 0.48f), new Vector2(0.95f, 0.76f), Palette.Gold);
            LocText.Bind(GachaUi.MakeButton(retry, "Retry1", string.Empty, new Vector2(0.05f, 0.08f), new Vector2(0.48f, 0.42f), Palette.NeonGreen, 22, () => Retry(1)),
                () => GachaConfirmPopup.CountLabel(1));
            _retryTen = GachaUi.MakeButton(retry, "Retry10", string.Empty, new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.42f), Palette.NeonGreen, 22, () => Retry(GachaService.TenPull));
            LocText.Bind(_retryTen, () => GachaConfirmPopup.CountLabel(GachaService.TenPull));
            var close = LocText.Bind(GachaUi.MakeButton(_donePanel, "Close", string.Empty, new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.15f), GachaUi.Muted, 22, Close),
                "ui.common.close");
            // 스킨: 완료 창은 popup_cyan, 다시 뽑기 칸은 info_box, 버튼은 뽑기·닫기 공통 그림.
            if (OripaLook.Apply(_donePanel.GetComponent<Image>(), "popup_cyan"))
            {
                OripaLook.Apply(retry.GetComponent<Image>(), "info_box");
                OripaLook.Button(retry.Find("Retry1").GetComponent<Button>(), "btn_pull", Palette.NeonGreen);
                OripaLook.Button(_retryTen, "btn_pull_ten", Palette.Cyan);
                OripaLook.Button(close, "btn_unequip", Palette.Text);
            }

            _root.gameObject.SetActive(false);
        }

        public void ShowConfirm(int pieces, int dollars, int balance)
        {
            _pieces = pieces;
            _confirmCount.text = Loc.F("ui.refund.count", pieces);
            _confirmBalance.text = GachaUi.Money(balance) + "  →  <color=" + GachaUi.Hex(Palette.NeonGreen) + ">" + GachaUi.Money(balance + dollars) + "</color>";
            if (_before != null)
            {
                _before.text = GachaUi.Money(balance);
                _after.text = GachaUi.Money(balance + dollars);
                TextOneLine.Fit(_before, 22, 13);
                TextOneLine.Fit(_after, 22, 13);
            }
            _confirmButton.interactable = true;
            _confirmPanel.gameObject.SetActive(true);
            _donePanel.gameObject.SetActive(false);
            Open(_confirmPanel);
        }

        public void LockConfirm()
        {
            _confirmButton.interactable = false;
        }

        public void HideConfirmPanel()
        {
            _confirmPanel.DOKill(false);
            var group = Group(_confirmPanel);
            group.DOKill(false);
            group.DOFade(0f, 0.15f).SetUpdate(true).SetLink(_confirmPanel.gameObject)
                .OnComplete(() => _confirmPanel.gameObject.SetActive(false));
        }

        public void ShowDone(GachaBanner banner, int before, int after)
        {
            _banner = banner;
            _doneBalance.text = GachaUi.Money(before) + "  →  <color=" + GachaUi.Hex(Palette.NeonGreen) + ">" + GachaUi.Money(after) + "</color>";
            WriteRetry();
            _retryTen.gameObject.SetActive(banner == null || banner.Offers(GachaService.TenPull));
            _confirmPanel.gameObject.SetActive(false);
            _donePanel.gameObject.SetActive(true);
            Open(_donePanel);
            Fx.Pop(_check);
        }

        public void Hide()
        {
            _group.DOKill(false);
            _root.gameObject.SetActive(false);
        }

        // 열려 있는 동안 언어가 바뀌면 값이 들어간 글자(개수·다시 뽑기 배너)만 다시 쓴다.
        public void Relabel()
        {
            if (!IsOpen)
            {
                return;
            }

            _confirmCount.text = Loc.F("ui.refund.count", _pieces);
            WriteRetry();
        }

        void WriteRetry()
        {
            _retryName.text = _banner != null ? _banner.Name : string.Empty;
            _retryPrice.text = _banner != null ? (_banner.Price > 0 ? Loc.F("ui.common.price_once", GachaUi.Money(_banner.Price)) : Loc.T("ui.common.free")) : string.Empty;
        }

        void Open(RectTransform panel)
        {
            Group(panel).alpha = 1f;
            _root.SetAsLastSibling();
            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
                _group.alpha = 0f;
                _group.DOKill(false);
                _group.DOFade(1f, 0.15f).SetUpdate(true).SetLink(_root.gameObject);
            }

            Fx.Pop(panel);
        }

        void HideConfirm()
        {
            Hide();
        }

        void Retry(int count)
        {
            var banner = _banner;
            Hide();
            if (banner != null)
            {
                _onRetry?.Invoke(banner, count);
            }
        }

        void Close()
        {
            Hide();
            _onClose?.Invoke();
        }

        static CanvasGroup Group(RectTransform panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            return group != null ? group : panel.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
