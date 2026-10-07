using UnityEngine;

namespace ScrapFishing.Core
{
    public static class DepthZone
    {
        public static int Index(float normalized)
        {
            if (normalized < 0.25f) return 0;
            if (normalized < 0.5f) return 1;
            if (normalized < 0.75f) return 2;
            return 3;
        }

        public static string Name(float normalized)
        {
            switch (Index(normalized))
            {
                case 0: return "표층";
                case 1: return "드론 묘지";
                case 2: return "슬러지";
                default: return "심연";
            }
        }

        public static float Meters(float normalized)
        {
            return 8f + Mathf.Clamp01(normalized) * 42f;
        }

        public static Color MarkerColor(float normalized)
        {
            switch (Index(normalized))
            {
                case 0: return new Color(0.7f, 0.7f, 0.62f);
                case 1: return Palette.Cyan;
                case 2: return Palette.Magenta;
                default: return Palette.NeonGreen;
            }
        }

        public static int SpawnCount(float normalized)
        {
            return 5 + Index(normalized);
        }

        public static int ExtraMines(float normalized)
        {
            return Index(normalized);
        }

        public static float DiveSeconds(float normalized)
        {
            return Mathf.Lerp(16f, 11f, Index(normalized) / 3f);
        }

        public static float MineDamage(float normalized)
        {
            return 0.14f + Index(normalized) * 0.04f;
        }

        public static int MinePenalty(float normalized)
        {
            return -(20 + Index(normalized) * 3);
        }

        public static Color WaterColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(0.10f, 0.28f, 0.42f);
                case 1: return new Color(0.06f, 0.16f, 0.36f);
                case 2: return new Color(0.14f, 0.07f, 0.28f);
                default: return Palette.DeepWater;
            }
        }

        public static float FromWorldY(float y)
        {
            return Mathf.InverseLerp(SurfaceLayout.WaterlineY, SurfaceLayout.MaxDepthY, y);
        }
    }
}
