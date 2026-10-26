using ScrapFishing.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 설정 패널의 음소거 버튼(스킨). 소리 켜짐/꺼짐에 따라 스피커 그림만 바꾼다.
    // HUD 음소거 버튼(SoundToggle)과 같은 설정을 쓰므로 어느 쪽에서 바꿔도 그림이 따라온다.
    public class SettingsSoundButton : MonoBehaviour
    {
        Image _image;
        Sprite _on;
        Sprite _off;

        public Image Image => _image;
        public bool ShowsOn => _image != null && _image.sprite == _on;

        public void Bind(Image image, Sprite on, Sprite off)
        {
            _image = image;
            _on = on;
            _off = off;
            AudioManager.SoundChanged -= Refresh;
            AudioManager.SoundChanged += Refresh;
            Refresh();
        }

        void OnEnable()
        {
            Refresh();
        }

        void OnDestroy()
        {
            AudioManager.SoundChanged -= Refresh;
        }

        void Refresh()
        {
            if (_image != null)
            {
                _image.sprite = AudioManager.Ensure().SoundOn ? _on : _off;
            }
        }
    }
}
