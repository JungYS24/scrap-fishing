using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Scrap
{
    public static class ScrapCatalog
    {
        static ScrapDefinition[][] _zones;

        public static ScrapDefinition Pick(float depth)
        {
            Ensure();
            var table = _zones[DepthZone.Index(depth)];
            var roll = Random.value;
            if (roll < 0.52f) return table[0];
            if (roll < 0.86f) return table[1];
            return table[2];
        }

        static void Ensure()
        {
            if (_zones != null)
            {
                return;
            }

            _zones = new[]
            {
                new[]
                {
                    ScrapDefinition.CreateRuntime("부유 합성수지", ScrapGrade.Junk, 8, new Color(0.62f, 0.62f, 0.55f)),
                    ScrapDefinition.CreateRuntime("깨진 네온 간판", ScrapGrade.Common, 18, Palette.Cyan),
                    ScrapDefinition.CreateRuntime("오염 피라미", ScrapGrade.Common, 22, new Color(0.45f, 0.85f, 0.4f))
                },
                new[]
                {
                    ScrapDefinition.CreateRuntime("드론 코어", ScrapGrade.Common, 28, Palette.Cyan),
                    ScrapDefinition.CreateRuntime("사이버 의수", ScrapGrade.Rare, 55, Palette.Magenta),
                    ScrapDefinition.CreateRuntime("네온 뱀장어", ScrapGrade.Rare, 62, Palette.NeonGreen)
                },
                new[]
                {
                    ScrapDefinition.CreateRuntime("화학 드럼", ScrapGrade.Rare, 70, new Color(0.85f, 0.55f, 0.15f)),
                    ScrapDefinition.CreateRuntime("데이터 칩", ScrapGrade.Rare, 80, Palette.Magenta),
                    ScrapDefinition.CreateRuntime("기계 아귀", ScrapGrade.Mutant, 95, Palette.NeonGreen)
                },
                new[]
                {
                    ScrapDefinition.CreateRuntime("AI 코어", ScrapGrade.Mutant, 110, Palette.Cyan),
                    ScrapDefinition.CreateRuntime("메카 잔해", ScrapGrade.Mutant, 125, Palette.Magenta),
                    ScrapDefinition.CreateRuntime("변이 크라켄", ScrapGrade.Mutant, 150, Palette.NeonGreen)
                }
            };
        }
    }
}
