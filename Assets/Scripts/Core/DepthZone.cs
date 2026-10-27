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
                case 0: return Loc.T("ui.zone.surface");
                case 1: return Loc.T("ui.zone.drone_graveyard");
                case 2: return Loc.T("ui.zone.sludge");
                default: return Loc.T("ui.zone.abyss");
            }
        }

        // 보트(낚시) 수심: 0 ~ 1000m. 구역 4단계가 250m씩 나뉜다(표층 ~250, 드론 묘지 ~500, 슬러지 ~750, 심연 ~1000).
        public const float MaxMeters = 1000f;

        public static float Meters(float normalized)
        {
            return Mathf.Clamp01(normalized) * MaxMeters;
        }

        // 잠수 구역: 낚시 4단계(심연, ~1000m)보다 아래인 해저. 수면 1500m ~ 바닥 1600m.
        public static string DiveZoneName => Loc.T("ui.zone.seabed_plain");
        public const float DiveTopMeters = 1500f;
        public const float DiveSpanMeters = 100f;

        public static float DiveMeters(float normalized)
        {
            return DiveTopMeters + Mathf.Clamp01(normalized) * DiveSpanMeters;
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
            return SpawnCount(normalized, 0);
        }

        // capBonus: 레전드 이상 루어의 스폰 상한 가산.
        public static int SpawnCount(float normalized, int capacity, int capBonus = 0)
        {
            var count = Mathf.Max(5 + Index(normalized), capacity + 2);
            return Mathf.Min(12 + Mathf.Max(0, capBonus), count);
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
