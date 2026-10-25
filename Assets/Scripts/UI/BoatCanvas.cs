using ScrapFishing.Boat;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class BoatCanvas : MonoBehaviour
    {
        [SerializeField] DepthGauge depthGauge;
        [SerializeField] Text chipText;
        [SerializeField] GameObject chipPanel;
        [SerializeField] GameObject readyPanel;
        [SerializeField] Text dollarText;
        [SerializeField] UpgradeButton[] upgradeButtons;
        [SerializeField] TitleView title;

        UiFxSettings _settings;
        CountingText _dollarCount;

        public DepthGauge DepthGauge => depthGauge;
        public Text ChipText => chipText;
        public GameObject ChipPanel => chipPanel;
        public TitleView Title => title;
        public RectTransform DollarPanel => dollarText != null ? dollarText.transform.parent as RectTransform : null;

        void OnEnable()
        {
            Progression.Changed += Refresh;
            EnsureFx();
            RefreshUpgrades();
        }

        void OnDisable()
        {
            Progression.Changed -= Refresh;
        }

        public void BringReadyToFront()
        {
            if (title != null)
            {
                title.transform.SetAsLastSibling();
            }

            if (readyPanel != null)
            {
                readyPanel.transform.SetAsLastSibling();
            }
        }

        // 달러 패널을 ReadyPanel(강화 버튼과 함께 출항 중 꺼짐) 밖으로 꺼내, 화면 위치는 그대로 두고
        // 타이틀·출항·잠수·결과·오리파 모든 단계에서 맨 앞에 보이게 한다.
        public void PinDollarPanel()
        {
            var panel = DollarPanel;
            if (panel == null || readyPanel == null)
            {
                return;
            }

            if (panel.parent == readyPanel.transform)
            {
                panel.SetParent(readyPanel.transform.parent, true);
            }

            if (panel.GetComponent<UiStayOnTop>() == null)
            {
                panel.gameObject.AddComponent<UiStayOnTop>();
            }

            panel.SetAsLastSibling();
        }

        public void SetReadyVisible(bool visible)
        {
            if (readyPanel == null)
            {
                return;
            }

            if (visible)
            {
                readyPanel.SetActive(true);
                RefreshUpgrades();
                PlayReadyIntro();
            }
            else
            {
                readyPanel.SetActive(false);
            }
        }

        void EnsureFx()
        {
            if (_settings == null)
            {
                _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            }

            if (dollarText != null && _dollarCount == null)
            {
                _dollarCount = dollarText.GetComponent<CountingText>();
                if (_dollarCount == null)
                {
                    _dollarCount = dollarText.gameObject.AddComponent<CountingText>();
                }

                _dollarCount.Bind(_settings, string.Empty, false, string.Empty, true);
            }
        }

        void PlayReadyIntro()
        {
            EnsureFx();
            if (_dollarCount != null)
            {
                _dollarCount.Snap(0);
                _dollarCount.PlayTo(Progression.Dollars);
            }

            if (upgradeButtons == null)
            {
                return;
            }

            var stagger = _settings != null ? _settings.ReadyStagger : 0.08f;
            for (var i = 0; i < upgradeButtons.Length; i++)
            {
                if (upgradeButtons[i] != null)
                {
                    upgradeButtons[i].PlayPop(i * stagger);
                }
            }
        }

        void Refresh()
        {
            EnsureFx();
            if (_dollarCount != null)
            {
                _dollarCount.PlayTo(Progression.Dollars);
            }
            else if (dollarText != null)
            {
                dollarText.text = Progression.Dollars.ToString();
            }

            RefreshUpgrades();
        }

        void RefreshUpgrades()
        {
            if (upgradeButtons == null)
            {
                return;
            }

            foreach (var upgrade in upgradeButtons)
            {
                if (upgrade != null)
                {
                    upgrade.Refresh();
                }
            }
        }
    }
}
