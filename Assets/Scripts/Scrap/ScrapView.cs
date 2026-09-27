using UnityEngine;

namespace ScrapFishing.Scrap
{
    public class ScrapView : MonoBehaviour
    {
        public ScrapDefinition Definition { get; private set; }

        SpriteRenderer _renderer;

        public void Bind(ScrapDefinition definition)
        {
            Definition = definition;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            _renderer.sprite = definition.Sprite != null
                ? definition.Sprite
                : PlaceholderFactory.Square(definition.PlaceholderColor);
            _renderer.color = definition.Sprite != null
                ? Color.Lerp(Color.white, definition.PlaceholderColor, 0.4f)
                : Color.white;
            _renderer.sortingOrder = 2;
        }
    }
}
