using UnityEngine;

namespace ScrapFishing.FX
{
    [CreateAssetMenu(menuName = "ScrapFishing/FXConfig")]
    public class FXConfig : ScriptableObject
    {
        public bool enabled = true;
        public int tweenCapacity = 200;
        public int sequenceCapacity = 50;
        public int maxBurstBits = 24;
        public int maxFloatTexts = 8;

        public float punchScale = 0.2f;
        public float punchDuration = 0.2f;
        public float popOvershoot = 1.15f;
        public float popDuration = 0.22f;
        public float flashDuration = 0.12f;
        public float floatRise = 0.7f;
        public float floatDuration = 0.55f;

        public float splashDrops = 7f;
        public float splashWeak = 0.55f;

        public float attachPunch = 0.3f;
        public float fullCooldown = 0.5f;
        public float fullNudge = 0.12f;

        public float swayBoost = 1f;
        public float rarePulseMin = 0.8f;
        public float rarePulseMax = 0.95f;

        public float settleJumpPunch = 0.18f;
        public float finaleShake = 0.1f;
        public float finaleShakeDur = 0.15f;

        public float countDuration = 0.4f;

        public float resultsStagger = 0.12f;
        public float fadeDuration = 0.25f;

        public float oxygenWarn = 0.2f;
        public float oxygenCritical = 0.1f;
        public float hurtShake = 0.25f;
        public float hurtShakeDur = 0.2f;

        static FXConfig _current;

        public static FXConfig Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<FXConfig>("FXConfig");
                    if (_current == null)
                    {
                        _current = CreateInstance<FXConfig>();
                    }
                }

                return _current;
            }
        }

        public static bool Enabled => Current != null && Current.enabled;
    }
}
