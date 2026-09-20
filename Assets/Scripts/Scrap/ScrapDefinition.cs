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
    }
}
