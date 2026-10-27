using System;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 번역 키(또는 글자를 만드는 함수)를 Text에 붙여 두고, 언어가 바뀌면 글자만 다시 넣는다.
    // 한 번 만들고 내용이 바뀌지 않는 라벨·버튼 글자용. 값이 계속 바뀌는 글자는 각 화면의 Refresh가 다시 쓴다.
    // 씬에 미리 만들어 둔 Text(GachaUi.Label이 그대로 돌려주는 것)도 Bind가 글자를 덮어쓴다.
    [RequireComponent(typeof(Text))]
    public class LocText : MonoBehaviour
    {
        Text _text;
        Func<string> _build;
        bool _subscribed;

        public static Text Bind(Text text, string key)
        {
            return Bind(text, () => Loc.T(key));
        }

        public static Text Bind(Text text, Func<string> build)
        {
            if (text == null || build == null)
            {
                return text;
            }

            var loc = text.GetComponent<LocText>();
            if (loc == null)
            {
                loc = text.gameObject.AddComponent<LocText>();
            }

            loc._text = text;
            loc._build = build;
            loc.Subscribe();
            loc.Apply();
            return text;
        }

        public static Button Bind(Button button, string key)
        {
            Bind(GachaUiLabel(button), key);
            return button;
        }

        public static Button Bind(Button button, Func<string> build)
        {
            Bind(GachaUiLabel(button), build);
            return button;
        }

        // 꺼져 있는 오브젝트도 바뀌어야 하므로 OnEnable이 아니라 만들 때 구독하고 파괴될 때 푼다.
        void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            Loc.Changed += Apply;
        }

        void OnDestroy()
        {
            if (_subscribed)
            {
                Loc.Changed -= Apply;
            }
        }

        public void Apply()
        {
            if (_text == null || _build == null)
            {
                return;
            }

            _text.text = _build();
            var fit = _text.GetComponent<TextOneLine>();
            if (fit != null)
            {
                fit.Refresh();
            }
        }

        static Text GachaUiLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }
    }
}
