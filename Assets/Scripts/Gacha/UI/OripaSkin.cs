using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // 오리파·보관함 UI 스킨 스프라이트(Assets/scrap_fishing_ui/oripa) 묶음. 화면은 실행 중에 코드로 만들어서
    // Resources/UI/OripaSkin 에셋 하나로 넘기고, 이름(파일 이름)으로 찾는다. 에셋이 없으면 예전 단색 그리기로 돈다.
    // 에셋은 Tools > Scrap Fishing > Create Oripa Skin으로 만든다.
    [CreateAssetMenu(menuName = "ScrapFishing/UI/OripaSkin")]
    public class OripaSkin : ScriptableObject
    {
        public const string Resource = "UI/OripaSkin";

        public List<Sprite> sprites = new List<Sprite>();

        Dictionary<string, Sprite> _byName;
        static OripaSkin _cached;
        static bool _searched;

        // 시험용: 스킨을 잠시 끄고(예전 모양) 확인할 때 쓴다.
        public static bool Disabled;

        public static OripaSkin Load()
        {
            if (Disabled)
            {
                return null;
            }

            if (!_searched)
            {
                _searched = true;
                _cached = Resources.Load<OripaSkin>(Resource);
            }

            return _cached;
        }

        public Sprite Get(string name)
        {
            if (_byName == null)
            {
                _byName = new Dictionary<string, Sprite>();
                foreach (var sprite in sprites)
                {
                    if (sprite != null)
                    {
                        _byName[sprite.name] = sprite;
                    }
                }
            }

            return name != null && _byName.TryGetValue(name, out var found) ? found : null;
        }
    }
}
