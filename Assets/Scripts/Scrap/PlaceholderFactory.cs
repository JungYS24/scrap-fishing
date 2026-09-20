using UnityEngine;

namespace ScrapFishing.Scrap
{
    public static class PlaceholderFactory
    {
        public static Sprite Square(Color color, int pixels = 16)
        {
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var fill = new Color[pixels * pixels];
            for (var i = 0; i < fill.Length; i++)
            {
                fill[i] = color;
            }

            texture.SetPixels(fill);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
        }

        public static Sprite Circle(Color color, int pixels = 32)
        {
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var fill = new Color[pixels * pixels];
            var radius = pixels * 0.5f - 0.5f;
            var center = pixels * 0.5f;
            for (var y = 0; y < pixels; y++)
            {
                for (var x = 0; x < pixels; x++)
                {
                    var dx = x + 0.5f - center;
                    var dy = y + 0.5f - center;
                    fill[y * pixels + x] = dx * dx + dy * dy <= radius * radius ? color : Color.clear;
                }
            }

            texture.SetPixels(fill);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
        }
    }
}
