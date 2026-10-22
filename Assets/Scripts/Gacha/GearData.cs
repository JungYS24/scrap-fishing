using System.Collections.Generic;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    [CreateAssetMenu(menuName = "ScrapFishing/Gacha/GearData")]
    public class GearData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] GearSlot slot;
        [SerializeField] ItemRarity rarity;
        [SerializeField] List<GearEffect> effects = new List<GearEffect>();
        [Tooltip("환원가 = 머신 1회 가격 × 이 값. 0 이하면 등급 기본값을 쓴다.")]
        [SerializeField] float refundMultiplier;
        [SerializeField] Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public GearSlot Slot => slot;
        public ItemRarity Rarity => rarity;
        public IReadOnlyList<GearEffect> Effects => effects;
        public float RefundMultiplier => refundMultiplier > 0f ? refundMultiplier : DefaultRefundMultiplier(rarity);
        public Sprite Icon => icon;

        public static float DefaultRefundMultiplier(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return 0.8f;
                case ItemRarity.Epic: return 3f;
                case ItemRarity.Legend: return 15f;
                case ItemRarity.Synchro: return 25f;
                default: return 0.3f;
            }
        }

        public float Value(GearStat stat)
        {
            var total = 0f;
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i].stat == stat)
                {
                    total += effects[i].value;
                }
            }

            return total;
        }

        public static GearData Create(string gearId, string name, GearSlot gearSlot, ItemRarity gearRarity, params GearEffect[] gearEffects)
        {
            var gear = CreateInstance<GearData>();
            gear.name = gearId;
            gear.id = gearId;
            gear.displayName = name;
            gear.slot = gearSlot;
            gear.rarity = gearRarity;
            gear.effects = new List<GearEffect>(gearEffects ?? new GearEffect[0]);
            return gear;
        }
    }
}
