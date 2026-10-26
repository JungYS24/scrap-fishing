using UnityEngine;

namespace ScrapFishing.UI
{
    // 설정 패널 스킨 스프라이트(Assets/scrap_fishing_ui/settings). 패널은 실행 중에 코드로 만들어서
    // Resources/UI/SettingsSkin 에셋 하나로 스프라이트를 넘긴다. 에셋이 없으면 예전 단색 그리기로 돈다.
    // 에셋은 Tools > Scrap Fishing > Create Settings Skin으로 만든다.
    [CreateAssetMenu(menuName = "ScrapFishing/UI/SettingsSkin")]
    public class SettingsSkin : ScriptableObject
    {
        public const string Resource = "UI/SettingsSkin";

        public Sprite panel;
        public Sprite headerBar;
        public Sprite close;
        public Sprite soundOn;
        public Sprite soundOff;
        public Sprite labelBox;
        public Sprite sliderTrack;
        public Sprite sliderFill;
        public Sprite sliderKnob;
        public Sprite wideCyan;
        public Sprite wideGray;
        public Sprite wideYellow;
        public Sprite wideGreen;
        public Sprite footerBox;

        static SettingsSkin _cached;

        public static SettingsSkin Load()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<SettingsSkin>(Resource);
            }

            return _cached;
        }

        public bool Complete =>
            panel != null && headerBar != null && close != null && soundOn != null && soundOff != null && labelBox != null &&
            sliderTrack != null && sliderFill != null && sliderKnob != null && wideCyan != null && wideGray != null &&
            wideYellow != null && wideGreen != null && footerBox != null;
    }
}
