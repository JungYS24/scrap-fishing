using DG.Tweening;
using UnityEngine;

namespace ScrapFishing.UI
{
    public static class UiFxBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var settings = Resources.Load<UiFxSettings>("UiFxSettings");
            var tweens = settings != null ? Mathf.Max(50, settings.tweenCapacity) : 400;
            var sequences = settings != null ? Mathf.Max(10, settings.sequenceCapacity) : 80;
            DOTween.Init(true, true, LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(tweens, sequences);
            DOTween.defaultAutoKill = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var reduced = settings != null && settings.Reduced;
            var chips = settings != null ? settings.MaxChips : 0;
            Debug.Log(
                "UiFx " + (reduced ? "reduce" : "full")
                + " tweens=" + tweens + "/" + sequences
                + " chips=" + chips
                + " mobile=" + Application.isMobilePlatform
                + " link+unscaled+pool");
#endif
        }
    }
}
