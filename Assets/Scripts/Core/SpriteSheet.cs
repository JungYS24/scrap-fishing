using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Core
{
    public static class SpriteSheet
    {
        const float PixelsPerUnit = 100f;

        static readonly Dictionary<Texture2D, Sprite[]> Cache = new Dictionary<Texture2D, Sprite[]>();

        public static Sprite[] Slice(Texture2D texture, int frames)
        {
            if (texture == null || frames <= 0)
            {
                return Array.Empty<Sprite>();
            }

            if (Cache.TryGetValue(texture, out var cached) && cached.Length == frames)
            {
                return cached;
            }

            var width = texture.width / frames;
            var sprites = new Sprite[frames];
            for (var i = 0; i < frames; i++)
            {
                var rect = new Rect(i * width, 0f, width, texture.height);
                sprites[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = $"{texture.name}_{i}";
            }

            Cache[texture] = sprites;
            return sprites;
        }
    }
}
