using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class UpgradeButton : MonoBehaviour
    {
        [SerializeField] UpgradeKind kind;
        [SerializeField] Button button;
        [SerializeField] Text nameText;
        [SerializeField] Text levelText;
        [SerializeField] Text costText;

        void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(Buy);
            }
        }

        void Buy()
        {
            if (Progression.TryBuy(kind))
            {
                AudioManager.Ensure().PlayCatch();
            }
        }

        public void Refresh()
        {
            var level = Progression.Level(kind);
            var maxed = Progression.IsMaxed(kind);
            var cost = Progression.Cost(kind);
            if (nameText != null)
            {
                nameText.text = Progression.DisplayName(kind);
            }

            if (levelText != null)
            {
                levelText.text = $"Lv {level}/{Progression.MaxLevel(kind)}";
            }

            if (costText != null)
            {
                costText.text = maxed ? "MAX" : $"$ {cost}";
            }

            if (button != null)
            {
                button.interactable = !maxed && Progression.Dollars >= cost;
            }
        }
    }
}
