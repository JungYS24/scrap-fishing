using ScrapFishing.Data;
using ScrapFishing.FX;
using UnityEngine;

namespace ScrapFishing.Scrap
{
    public class ScrapView : MonoBehaviour
    {
        public ItemData data;
        public ScrapDefinition Definition { get; private set; }

        SpriteRenderer _renderer;
        int amount;

        public bool Attached { get; set; }
        public SpriteRenderer Renderer => _renderer != null ? _renderer : GetComponent<SpriteRenderer>();
        public int Amount => ChipValue;
        public int ChipValue
        {
            get
            {
                if (data != null)
                {
                    return data.Chip;
                }

                if (Definition != null)
                {
                    return Definition.Value;
                }

                return amount;
            }
        }

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

            var sprite = spriteOverride != null ? spriteOverride : definition != null ? definition.Sprite : null;
            if (sprite == null && data != null)
            {
                sprite = data.Icon;
            }

            _renderer.sprite = sprite != null
                ? sprite
                : PlaceholderFactory.Square(definition != null ? definition.PlaceholderColor : Color.white);
            _renderer.color = Color.white;
            _renderer.sortingOrder = 2;
            RarityGlow.Apply(this);
        }

        public void Bind(ItemData item)
        {
            data = item;
            Bind(ScrapDefinition.FromItem(item), item != null ? item.Icon : null);
        }

        public void ApplyHang(float alpha, Vector3 scale, int sortingOrder)
        {
            _renderer = Renderer;
            if (_renderer != null)
            {
                var color = _renderer.color;
                color.a = alpha;
                _renderer.color = color;
                _renderer.sortingOrder = sortingOrder;
            }

            transform.localScale = scale;
        }

        public void RestoreLook(Color color, Vector3 scale, int sortingOrder)
        {
            _renderer = Renderer;
            if (_renderer != null)
            {
                _renderer.color = color;
                _renderer.sortingOrder = sortingOrder;
            }

            transform.localScale = scale;
        }
    }
}
