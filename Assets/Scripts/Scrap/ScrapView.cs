using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Scrap
{
    public class ScrapView : MonoBehaviour
    {
        public ItemData data;
        public ScrapDefinition Definition { get; private set; }

        SpriteRenderer _renderer;
        int amount;

        public int Amount => data != null ? data.Chip : amount;

        void Awake()
        {
            if (data != null)
            {
                amount = data.Chip;
            }
        }

        public void Bind(ScrapDefinition definition, Sprite spriteOverride = null)
        {
            Definition = definition;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            var sprite = spriteOverride != null ? spriteOverride : definition.Sprite;
            _renderer.sprite = sprite != null
                ? sprite
                : PlaceholderFactory.Square(definition.PlaceholderColor);
            _renderer.color = sprite != null
                ? Color.Lerp(Color.white, definition.PlaceholderColor, 0.25f)
                : Color.white;
            _renderer.sortingOrder = 2;
        }
    }
}
