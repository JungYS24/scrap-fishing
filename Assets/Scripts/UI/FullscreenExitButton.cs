using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 설정 패널 음소거 버튼 왼쪽의 전체화면 해제 버튼. 그림은 페이지의 전체화면 버튼(Resources/UI/icon_fullscreen)과 같다.
    // 전체화면은 페이지(index.html)가 켜므로 게임은 상태만 읽고 끈다(Scripts/WebGL/Fullscreen.jslib).
    // 전체화면일 때만 보이고, 뒤로 가기 등으로 풀려도 매 프레임 확인해 바로 숨긴다. 에디터·데스크톱에서는 항상 숨는다.
    public class FullscreenExitButton : MonoBehaviour
    {
        public const string IconResource = "UI/icon_fullscreen";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int ScrapFishing_IsFullscreen();

        [DllImport("__Internal")]
        static extern void ScrapFishing_ExitFullscreen();
#endif

        Image _image;
        Button _button;

        public static bool IsFullscreen
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return ScrapFishing_IsFullscreen() != 0;
#else
                return false;
#endif
            }
        }

        public static void Exit()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ScrapFishing_ExitFullscreen();
#endif
        }

        public void Bind(Image image, Button button)
        {
            _image = image;
            _button = button;
            Refresh();
        }

        void OnEnable()
        {
            Refresh();
        }

        void Update()
        {
            Refresh();
        }

        // 오브젝트를 끄면 Update도 멈추므로 그림·클릭만 끈다.
        void Refresh()
        {
            if (_image == null)
            {
                return;
            }

            var show = IsFullscreen;
            if (_image.enabled != show)
            {
                _image.enabled = show;
                _button.interactable = show;
            }
        }
    }
}
