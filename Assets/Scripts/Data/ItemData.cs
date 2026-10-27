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
        Normal,
        Rare,
        Epic,
        Legend,
        Synchro
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
        [Tooltip("에픽 이상 루프 애니메이션 프레임(시트에서 잘린 순서). 노멀·레어는 비어 있다.")]
        [SerializeField] Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] float frameRate = 8f;
        [SerializeField] GameObject prefab;

        public string Id => id;
        // 화면 이름은 번역 표(scrap.<id>.name)를 먼저 보고, 없으면 임포트한 이름 그대로.
        public string DisplayName => ScrapFishing.Core.Loc.Or("scrap." + id + ".name", displayName);
        public ItemCategory Category => category;
        public ItemRarity Rarity => rarity;
        public string Zone => zone;
        public int Chip => chip;
        public int Gold => gold;
        public Sprite Icon => icon;
        public bool HasAnimation => hasAnimation;
        public AnimationClip AnimationClip => animationClip;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public Sprite[] Frames => frames ?? System.Array.Empty<Sprite>();
        public float FrameRate => frameRate > 0f ? frameRate : 8f;
        public bool HasFrames => frames != null && frames.Length > 1;
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

        public void SetFrames(Sprite[] itemFrames)
        {
            frames = itemFrames ?? System.Array.Empty<Sprite>();
        }

        public void SetPrefab(GameObject itemPrefab)
        {
            prefab = itemPrefab;
        }
    }
}
