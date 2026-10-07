using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Dive
{
    [CreateAssetMenu(menuName = "Scrap Fishing/Dive Art", fileName = "DiveArt")]
    public class DiveArt : ScriptableObject
    {
        public Texture2D FishSmall;
        public Texture2D FishDart;
        public Texture2D FishBig;
        public Texture2D PlayerIdle;
        public Texture2D PlayerSwim;
        public Texture2D PlayerHurt;
        public Texture2D Bubbles;
        public Texture2D ExplosionSmall;
        public Texture2D ExplosionMedium;
        public Texture2D ExplosionBig;
        public Sprite MineSmall;
        public Sprite MineMedium;
        public Sprite MineBig;

        public Sprite[] Idle => SpriteSheet.Slice(PlayerIdle, 6);
        public Sprite[] Swim => SpriteSheet.Slice(PlayerSwim, 7);
        public Sprite[] Hurt => SpriteSheet.Slice(PlayerHurt, 5);
        public Sprite[] BubbleFrames => SpriteSheet.Slice(Bubbles, 4);

        public Sprite[] Fish(int kind)
        {
            switch (kind)
            {
                case 1: return SpriteSheet.Slice(FishDart, 4);
                case 2: return SpriteSheet.Slice(FishBig, 4);
                default: return SpriteSheet.Slice(FishSmall, 4);
            }
        }

        public static int FishKind(ScrapGrade grade)
        {
            switch (grade)
            {
                case ScrapGrade.Rare: return 1;
                case ScrapGrade.Mutant: return 2;
                default: return 0;
            }
        }

        public Sprite Mine(int size)
        {
            switch (size)
            {
                case 0: return MineSmall;
                case 2: return MineBig;
                default: return MineMedium;
            }
        }

        public Sprite[] Explosion(int size)
        {
            switch (size)
            {
                case 0: return SpriteSheet.Slice(ExplosionSmall, 11);
                case 2: return SpriteSheet.Slice(ExplosionBig, 11);
                default: return SpriteSheet.Slice(ExplosionMedium, 11);
            }
        }
    }
}
