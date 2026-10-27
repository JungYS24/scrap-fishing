using ScrapFishing.Core;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Scrap
{
    [CreateAssetMenu(menuName = "Scrap Fishing/Scrap Definition", fileName = "ScrapDefinition")]
    public class ScrapDefinition : ScriptableObject
    {
        public string DisplayName;
        // 번역 키(scrap.<id>.name, scrap.fallback.NN). 화면에는 Label을 쓴다. 키가 없으면 DisplayName 그대로.
        public string NameKey;
        public ScrapGrade Grade;
        public int Value;
        public Sprite Sprite;
        public Color PlaceholderColor = Color.white;

        public string Label => Loc.Or(NameKey, DisplayName);

        public static string KeyOf(ItemData data)
        {
            return data != null && !string.IsNullOrEmpty(data.Id) ? "scrap." + data.Id + ".name" : null;
        }

        public static ScrapDefinition CreateRuntime(string displayName, ScrapGrade grade, int value, Color color, string nameKey = null)
        {
            var definition = CreateInstance<ScrapDefinition>();
            definition.DisplayName = displayName;
            definition.NameKey = nameKey;
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

            var definition = CreateRuntime(data.DisplayName, GradeOf(data.Rarity), data.Chip, Color.white, KeyOf(data));
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
                case ItemRarity.Synchro:
                    return ScrapGrade.Mutant;
                default:
                    return ScrapGrade.Common;
            }
        }
    }
}
