using UnityEngine;

namespace ScrapFishing.Data
{
    public enum ItemCategory
    {
        Fish,
        Scrap
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(menuName = "ScrapFishing/ItemData")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] ItemCategory category;
        [SerializeField] ItemRarity rarity;
        [SerializeField] string zone;
        [SerializeField] int chip;
        [SerializeField] int gold;
        [SerializeField] Sprite icon;
        [SerializeField] bool hasAnimation;
        [SerializeField] AnimationClip animationClip;
        [SerializeField] RuntimeAnimatorController animatorController;
        [SerializeField] GameObject prefab;

        public string Id => id;
        public string DisplayName => displayName;
        public ItemCategory Category => category;
        public ItemRarity Rarity => rarity;
        public string Zone => zone;
        public int Chip => chip;
        public int Gold => gold;
        public Sprite Icon => icon;
        public bool HasAnimation => hasAnimation;
        public AnimationClip AnimationClip => animationClip;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public GameObject Prefab => prefab;

        public void ApplyImported(
            string itemId,
            string itemName,
            ItemCategory itemCategory,
            ItemRarity itemRarity,
            string itemZone,
            int itemChip,
            int itemGold,
            Sprite itemIcon,
            bool itemHasAnimation,
            AnimationClip clip,
            RuntimeAnimatorController controller)
        {
            id = itemId;
            displayName = itemName;
            category = itemCategory;
            rarity = itemRarity;
            zone = itemZone;
            chip = itemChip;
            gold = itemGold;
            icon = itemIcon;
            hasAnimation = itemHasAnimation;
            animationClip = clip;
            animatorController = controller;
        }

        public void SetPrefab(GameObject itemPrefab)
        {
            prefab = itemPrefab;
        }
    }
}
