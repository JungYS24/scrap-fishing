using UnityEngine;

namespace ScrapFishing.UI
{
    // 첫 출항 튜토리얼 스킨(Assets/scrap_fishing_ui/tutorial). 튜토리얼은 실행 중에 코드로 만들어서
    // Resources/UI/TutorialSkin 에셋 하나로 스프라이트를 넘긴다. 빠진 스프라이트는 FirstPlayGuide가 예전 모양(단색 패널·노란 사각형)으로 그린다.
    // 에셋은 PNG를 가져오면 자동으로 다시 채워지고, Tools > Scrap Fishing > Create Tutorial Skin으로도 만든다.
    [CreateAssetMenu(menuName = "ScrapFishing/UI/TutorialSkin")]
    public class TutorialSkin : ScriptableObject
    {
        public const string Resource = "UI/TutorialSkin";

        public Sprite panel;
        public Sprite tab;
        public Sprite button;
        public Sprite dotOn;
        public Sprite dotOff;
        public Sprite bracket;
        public Sprite glow;
        public Sprite arrowRight;
        public Sprite arrowDown;
        public Sprite hand;
        public Sprite ring;

        static TutorialSkin _cached;

        public static TutorialSkin Load()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<TutorialSkin>(Resource);
            }

            return _cached;
        }

        public bool Complete =>
            panel != null && tab != null && button != null && dotOn != null && dotOff != null && bracket != null &&
            glow != null && arrowRight != null && arrowDown != null && hand != null && ring != null;
    }
}
