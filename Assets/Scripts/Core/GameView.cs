using UnityEngine;

namespace ScrapFishing.Core
{
    public static class GameView
    {
        public const int Width = 480;
        public const int Height = 854;
        public const float Aspect = Width / (float)Height;
        public const float PixelsPerUnit = 100f;
        public const float OrthoSize = Height / (PixelsPerUnit * 2f);
    }
}
