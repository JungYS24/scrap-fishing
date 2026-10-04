using UnityEngine;

namespace ScrapFishing.Scrap
{
    public class ScrapView : MonoBehaviour
    {
        public ScrapDefinition Definition { get; private set; }

        SpriteRenderer _renderer;

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
                ? Color.Lerp(Color.white, definition.PlaceholderColor, 0.4f)
                : Color.white;
            _renderer.sortingOrder = 2;
        }
    }
}
