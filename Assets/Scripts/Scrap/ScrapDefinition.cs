using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Scrap
{
    [CreateAssetMenu(menuName = "Scrap Fishing/Scrap Definition", fileName = "ScrapDefinition")]
    public class ScrapDefinition : ScriptableObject
    {
        public string DisplayName;
        public ScrapGrade Grade;
        public int Value;
        public Sprite Sprite;
        public Color PlaceholderColor = Color.white;

        public static ScrapDefinition CreateRuntime(string displayName, ScrapGrade grade, int value, Color color)
        {
            var definition = CreateInstance<ScrapDefinition>();
            definition.DisplayName = displayName;
            definition.Grade = grade;
            definition.Value = value;
            definition.PlaceholderColor = color;
            return definition;
        }

        public static ScrapDefinition FromItem(ItemData data)
        {
            if (data == null)
            {
                return CreateRuntime("scrap", ScrapGrade.Junk, 8, Color.white);
            }

            var definition = CreateRuntime(data.DisplayName, GradeOf(data.Rarity), data.Chip, Color.white);
            definition.Sprite = data.Icon;
            return definition;
        }

        static ScrapGrade GradeOf(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare:
                    return ScrapGrade.Rare;
                case ItemRarity.Epic:
                case ItemRarity.Legend:
                    return ScrapGrade.Mutant;
                default:
                    return ScrapGrade.Common;
            }
        }
    }
}
