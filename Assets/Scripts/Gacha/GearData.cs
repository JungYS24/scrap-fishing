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
        [SerializeField] string description;
        [SerializeField] GearSlot slot;
        [SerializeField] ItemRarity rarity;
        [SerializeField] List<GearEffect> effects = new List<GearEffect>();
        [Tooltip("환원가 = 머신 1회 가격 × 이 값. 0 이하면 등급 기본값을 쓴다.")]
        [SerializeField] float refundMultiplier;
        [SerializeField] Sprite icon;
        [Tooltip("보트 씬 조합용 256 레이어. 보트·피셔만 있다.")]
        [SerializeField] Sprite layer;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public GearSlot Slot => slot;
        public ItemRarity Rarity => rarity;
        public IReadOnlyList<GearEffect> Effects => effects;
        public float RefundMultiplier => refundMultiplier > 0f ? refundMultiplier : DefaultRefundMultiplier(rarity);
        public Sprite Icon => icon;
        public Sprite Layer => layer;

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

        public void SetDetails(string desc, float refund, Sprite sprite)
        {
            description = desc;
            refundMultiplier = refund;
            icon = sprite;
        }

        public void SetLayer(Sprite sprite)
        {
            layer = sprite;
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
