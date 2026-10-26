using System;
using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.FX;
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

        public bool IsOpen => _root.gameObject.activeSelf;

        public GachaRefundPopup(RectTransform parent, Action<RectTransform> onConfirm, Action<GachaBanner, int> onRetry, Action onClose)
        {
            _onConfirm = onConfirm;
            _onRetry = onRetry;
            _onClose = onClose;
            _root = GachaUi.Panel(parent, "GachaRefund", Vector2.zero, new Vector2(1f, 0.925f), GachaUi.Dim, null, true).rectTransform;
            _group = GachaUi.GetOrAdd<CanvasGroup>(_root);

            _confirmPanel = GachaUi.Panel(_root, "ConfirmPanel", new Vector2(0.07f, 0.3f), new Vector2(0.93f, 0.7f), GachaUi.Card, Palette.Gold, true).rectTransform;
            GachaUi.Label(_confirmPanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.8f), new Vector2(0.96f, 0.96f), Palette.Gold, "달러 환원");
            _confirmCount = GachaUi.Label(_confirmPanel, "Count", 22, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.64f), new Vector2(0.96f, 0.78f), Palette.Text);
            _confirmBalance = GachaUi.Label(_confirmPanel, "Balance", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.45f), new Vector2(0.96f, 0.62f), Palette.Text);
            GachaUi.Label(_confirmPanel, "Note", 16, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.33f), new Vector2(0.96f, 0.44f), GachaUi.Muted,
                "환원한 장비는 되돌릴 수 없습니다");
            GachaUi.MakeButton(_confirmPanel, "Cancel", "취소", new Vector2(0.05f, 0.06f), new Vector2(0.45f, 0.28f), GachaUi.Muted, 22, HideConfirm);
            _confirmButton = GachaUi.MakeButton(_confirmPanel, "Confirm", "확정하기", new Vector2(0.5f, 0.06f), new Vector2(0.95f, 0.28f), Palette.NeonGreen, 22,
                () => _onConfirm?.Invoke((RectTransform)_confirmButton.transform));

            _donePanel = GachaUi.Panel(_root, "DonePanel", new Vector2(0.07f, 0.22f), new Vector2(0.93f, 0.78f), GachaUi.Card, Palette.NeonGreen, true).rectTransform;
            _check = GachaUi.CheckMark(_donePanel, "Check", new Vector2(0.4f, 0.79f), new Vector2(0.6f, 0.95f), Palette.NeonGreen);
            GachaUi.Label(_donePanel, "Title", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.78f), Palette.NeonGreen, "달러로 교환했어요");
            _doneBalance = GachaUi.Label(_donePanel, "Balance", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.56f), new Vector2(0.96f, 0.67f), Palette.Text);

            var retry = GachaUi.Panel(_donePanel, "Retry", new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.52f), GachaUi.Inset, Palette.Magenta).rectTransform;
            GachaUi.Label(retry, "Caption", 16, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.96f), GachaUi.Muted, "다시 뽑기");
            _retryName = GachaUi.Label(retry, "Name", 22, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.48f), new Vector2(0.6f, 0.76f), Palette.Text);
            _retryPrice = GachaUi.Label(retry, "Price", 16, TextAnchor.MiddleRight, new Vector2(0.55f, 0.48f), new Vector2(0.95f, 0.76f), Palette.Gold);
            GachaUi.MakeButton(retry, "Retry1", "1회", new Vector2(0.05f, 0.08f), new Vector2(0.48f, 0.42f), Palette.NeonGreen, 22, () => Retry(1));
            _retryTen = GachaUi.MakeButton(retry, "Retry10", "10연", new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.42f), Palette.NeonGreen, 22, () => Retry(GachaService.TenPull));
            GachaUi.MakeButton(_donePanel, "Close", "닫기", new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.15f), GachaUi.Muted, 22, Close);
            _root.gameObject.SetActive(false);
        }

        public void ShowConfirm(int pieces, int dollars, int balance)
        {
            _confirmCount.text = "선택한 장비 " + pieces + "개를 환원합니다";
            _confirmBalance.text = GachaUi.Money(balance) + "  →  <color=" + GachaUi.Hex(Palette.NeonGreen) + ">" + GachaUi.Money(balance + dollars) + "</color>";
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
            _retryName.text = banner != null ? banner.NameKo : string.Empty;
            _retryPrice.text = banner != null ? (banner.Price > 0 ? "1회 " + GachaUi.Money(banner.Price) : "무료") : string.Empty;
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
