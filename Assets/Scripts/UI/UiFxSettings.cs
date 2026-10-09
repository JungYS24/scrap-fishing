using DG.Tweening;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.UI
{
    [CreateAssetMenu(menuName = "Scrap Fishing/UI FX Settings", fileName = "UiFxSettings")]
    public class UiFxSettings : ScriptableObject
    {
        [Header("Count")]
        public float countDuration = 0.38f;
        public Ease countEase = Ease.OutQuad;
        public float punchScale = 0.14f;
        public float punchDuration = 0.16f;
        public Color gainFlash = new Color(1f, 1f, 0.85f);
        public Color loseFlash = new Color(1f, 0.28f, 0.28f);
        public float flashDuration = 0.12f;
        public float shakeDuration = 0.22f;
        public float shakeStrength = 8f;

        [Header("Chip fly")]
        public Sprite chipSprite;
        public Color chipColor = new Color(1f, 0.85f, 0.25f);
        public Color rareFlash = Color.white;
        public int minChips = 3;
        public int junkChips = 3;
        public int commonChips = 5;
        public int rareChips = 7;
        public int mutantChips = 10;
        public int maxActiveChips = 40;
        public float chipSize = 34f;
        public float popDuration = 0.15f;
        public float scatterDuration = 0.16f;
        public float scatterDistance = 46f;
        public float holdDuration = 0.07f;
        public float flyDuration = 0.4f;
        public float stagger = 0.045f;
        public float arrivePunch = 0.18f;
        public float loseFall = 140f;

        [Header("Toast")]
        public float toastPopDuration = 0.12f;
        public float toastHoldDuration = 0.35f;
        public float toastRiseDuration = 0.4f;
        public float toastRise = 52f;

        [Header("Mutant banner")]
        public float bannerInDuration = 0.18f;
        public float bannerHoldDuration = 0.55f;
        public float bannerOutDuration = 0.2f;

        [Header("Title")]
        public float titleFadeDuration = 0.28f;
        public float titleLogoDuration = 0.4f;
        public float titleButtonDuration = 0.22f;
        public float titleHideDuration = 0.18f;
        public float titlePulseScale = 0.06f;
        public float titlePulseDuration = 0.75f;

        [Header("Ready")]
        public float readyStagger = 0.08f;
        public float readyPopDuration = 0.22f;

        [Header("HUD")]
        public float hudFadeDuration = 0.22f;
        public float hudSlide = 24f;

        [Header("Results")]
        public float resultsFadeDuration = 0.25f;
        public float resultsBestDuration = 0.35f;
        public float resultsButtonDuration = 0.22f;

        [Header("Capacity")]
        public int tweenCapacity = 400;
        public int sequenceCapacity = 80;

        [Header("Reduce FX")]
        public bool reduceOnMobile = true;
        public bool forceReduce;
        public int reducedMaxActiveChips = 16;
        public int reducedMinChips = 2;

        public bool Reduced => forceReduce || reduceOnMobile && Application.isMobilePlatform;
        public int MaxChips => Reduced ? reducedMaxActiveChips : maxActiveChips;
        public float ReadyStagger => Reduced ? 0f : readyStagger;

        public int ChipCount(ScrapGrade grade, int value)
        {
            var byGrade = junkChips;
            if (grade == ScrapGrade.Common)
            {
                byGrade = commonChips;
            }
            else if (grade == ScrapGrade.Rare)
            {
                byGrade = rareChips;
            }
            else if (grade == ScrapGrade.Mutant)
            {
                byGrade = mutantChips;
            }

            var byValue = Mathf.RoundToInt(Mathf.Abs(value) / 12f);
            var count = Mathf.Clamp(Mathf.Max(byGrade, byValue), minChips, mutantChips);
            if (Reduced)
            {
                count = Mathf.Clamp((count + 1) / 2, reducedMinChips, MaxChips);
            }

            return Mathf.Min(count, MaxChips);
        }
    }
}
