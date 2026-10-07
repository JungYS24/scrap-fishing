using UnityEngine;

namespace ScrapFishing.Core
{
    public static class SurfaceLayout
    {
        public const float WaterlineY = -0.2f;
        public const float MaxDepthY = -22f;
        public const float HookX = 0.45f;

        public static readonly Vector3 RodTip = new Vector3(-1.05f, 0.12f, 0f);
        public static readonly Vector3 Barge = new Vector3(-1.45f, -0.08f, 0f);
        public static readonly Vector3 Fisher = new Vector3(-1.58f, 0.2f, 0f);
    }
}
