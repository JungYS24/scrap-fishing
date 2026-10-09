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
