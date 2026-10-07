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
        [SerializeField] GameObject readyPanel;
        [SerializeField] Text dollarText;
        [SerializeField] UpgradeButton[] upgradeButtons;

        public DepthGauge DepthGauge => depthGauge;
        public Text ChipText => chipText;

        void OnEnable()
        {
            Progression.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Progression.Changed -= Refresh;
        }

        public void BringReadyToFront()
        {
            if (readyPanel != null)
            {
                readyPanel.transform.SetAsLastSibling();
            }
        }

        public void SetReadyVisible(bool visible)
        {
            if (readyPanel != null)
            {
                readyPanel.SetActive(visible);
            }

            if (visible)
            {
                Refresh();
            }
        }

        void Refresh()
        {
            if (dollarText != null)
            {
                dollarText.text = $"$ {Progression.Dollars}";
            }

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
