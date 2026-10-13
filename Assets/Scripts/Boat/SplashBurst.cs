using ScrapFishing.Core;
using ScrapFishing.FX;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public static class SplashBurst
    {
        public static void Spawn(Vector3 at)
        {
            Fx.Splash(at, 1f);
        }

        public static void SpawnWeak(Vector3 at)
        {
            Fx.Splash(at, FXConfig.Current.splashWeak);
        }
    }
}
