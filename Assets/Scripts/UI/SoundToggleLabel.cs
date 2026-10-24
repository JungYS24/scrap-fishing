using ScrapFishing.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 음소거 버튼 아이콘(♪/×)을 실제 음소거 상태에 맞춘다. 다른 곳에서 바꿔도 따라 바뀐다.
    public class SoundToggleLabel : MonoBehaviour
    {
        Text _label;

        public void Bind(Text label)
        {
            _label = label;
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
            SoundToggle.Refresh(_label);
        }
    }
}
