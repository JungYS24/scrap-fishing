using UnityEngine;

namespace ScrapFishing.UI
{
    // 인게임 HUD 패널 스킨(Assets/scrap_fishing_ui/hud). HUD는 실행 중에 코드로 만들어서
    // Resources/UI/HudSkin 에셋 하나로 스프라이트·글자 영역·크기를 넘긴다.
    // 에셋은 Tools > Scrap Fishing > Create HUD Skin으로 만들고, 글자 영역은 hud_layout.json에서 옮겨 온다.
    [CreateAssetMenu(menuName = "ScrapFishing/UI/HudSkin")]
    public class HudSkin : ScriptableObject
    {
        public const string Resource = "UI/HudSkin";

        [Tooltip("HUD 패널 전체 크기 배율. 1이면 480×854 기준 수심 124px·스크랩 112px·TIME 86px·잠수 칸 34px 폭.")]
        [Range(0.5f, 2f)] public float hudScale = 1f;

        public Sprite depthPanel;
        public Sprite zoneTag;
        public Sprite scrapPanel;
        public Sprite scrapPanelFull;
        public Sprite scrapBarFill;
        public Sprite timePanel;
        public Sprite timePanelWarning;
        public Sprite diveFull;
        public Sprite diveEmpty;

        [Header("글자·채움 영역(패널 안 0~1 앵커, 왼쪽 아래 원점)")]
        public Rect depthNumber = Rect.MinMaxRect(0.305f, 0.171f, 0.837f, 0.8f);
        public Rect timeNumber = Rect.MinMaxRect(0.354f, 0.157f, 0.951f, 0.824f);
        public Rect scrapNumber = Rect.MinMaxRect(0.341f, 0.315f, 0.951f, 0.899f);
        public Rect scrapBar = Rect.MinMaxRect(0.077f, 0.135f, 0.923f, 0.236f);
        public Rect zoneText = Rect.MinMaxRect(0.069f, 0.152f, 0.931f, 0.818f);

        static HudSkin _cached;

        public static HudSkin Load()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<HudSkin>(Resource);
            }

            return _cached;
        }

        public bool Complete =>
            depthPanel != null && zoneTag != null && scrapPanel != null && scrapPanelFull != null && scrapBarFill != null &&
            timePanel != null && timePanelWarning != null && diveFull != null && diveEmpty != null;
    }
}
