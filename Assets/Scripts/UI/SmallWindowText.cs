using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 가로 창(예: 800×450, 게임이 0.53배로 줄어듦)에서도 핵심 글씨가 화면에서 MinPixels 이상이 되게 글자 크기를 올린다.
    // 핵심 글씨: HUD·달러·칩 숫자, 튜토리얼, 버튼·탭 글씨. 창이 커지면 원래 크기로 돌아간다.
    // 오리파 확인 팝업의 안내 줄(환급·확률·주의)은 19pt로 키우면 칸이 겹쳐서 원래 크기로 둔다(버튼 글씨만 키운다).
    // 코드가 나중에 글자 크기를 바꾸면(언어 전환, 한 줄 맞춤 등) 그 값을 새 원래 크기로 삼는다.
    // CrazyGames 빌드에서만 켠다(세로 모바일 배포는 원래 크기 그대로). 테스트는 ForceEnabled로 켠다.
    public class SmallWindowText : MonoBehaviour
    {
        public const float MinPixels = 10f;
        const float Interval = 0.25f;

        static readonly string[] KeyRoots = { "Hud", "Dollor_Panel", "Chip_Panel", "FirstPlayGuide" };

        public static bool ForceEnabled;

        public static bool Enabled
        {
            get
            {
#if CRAZYGAMES
                return true;
#else
                return ForceEnabled;
#endif
            }
        }

        class Entry
        {
            public int Applied;
            public int Size;
            public int Min;
            public int Max;
            public VerticalWrapMode Vertical;
        }

        readonly Dictionary<Text, Entry> _entries = new Dictionary<Text, Entry>();
        readonly List<Text> _texts = new List<Text>();
        RectTransform _area;
        Canvas _canvas;
        float _next;

        public void Bind(RectTransform area)
        {
            _area = area;
            _canvas = area != null ? area.GetComponentInParent<Canvas>() : null;
            _next = 0f;
        }

        void LateUpdate()
        {
            if (_area == null || _canvas == null || Time.unscaledTime < _next)
            {
                return;
            }

            _next = Time.unscaledTime + Interval;
            Apply();
        }

        public void Apply()
        {
            var root = _canvas.rootCanvas;
            var rootScale = root.transform.lossyScale.y;
            if (rootScale < 1e-5f)
            {
                return;
            }

            var areaScale = _area.lossyScale.y;
            _area.GetComponentsInChildren(false, _texts);
            foreach (var text in _texts)
            {
                if (!IsKey(text.transform))
                {
                    continue;
                }

                // 이 글씨 1단위가 화면에서 몇 픽셀인지(부모 크기·캔버스 배율 포함). 등장 연출로 잠깐 작아진 크기는 무시하고
                // 게임 영역 배율 아래로는 보지 않는다(안 그러면 연출 중에 글씨가 잠깐 커진다).
                var unitPixels = Mathf.Max(text.transform.lossyScale.y, areaScale) / rootScale * root.scaleFactor;
                if (unitPixels < 1e-3f)
                {
                    continue;
                }

                if (!_entries.TryGetValue(text, out var entry))
                {
                    entry = new Entry { Applied = -1 };
                    _entries[text] = entry;
                }

                if (text.fontSize != entry.Applied)
                {
                    entry.Size = text.fontSize;
                    entry.Min = text.resizeTextMinSize;
                    entry.Max = text.resizeTextMaxSize;
                    entry.Vertical = text.verticalOverflow;
                }

                var need = Mathf.CeilToInt(MinPixels / unitPixels - 0.01f);
                var size = Mathf.Max(entry.Size, need);
                if (text.resizeTextForBestFit)
                {
                    var min = Mathf.Max(entry.Min, need);
                    text.resizeTextMinSize = min;
                    text.resizeTextMaxSize = Mathf.Max(entry.Max, min);
                }

                // 칸보다 커진 글씨가 잘려 사라지지 않게(Truncate는 칸에 안 들어가는 줄을 통째로 숨긴다) 넘치게 둔다.
                text.verticalOverflow = size > entry.Size ? VerticalWrapMode.Overflow : entry.Vertical;
                text.fontSize = size;
                entry.Applied = size;
            }

            _texts.Clear();
        }

        static bool IsKey(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.GetComponent<Selectable>() != null)
                {
                    return true;
                }

                var name = p.name;
                for (var i = 0; i < KeyRoots.Length; i++)
                {
                    if (name == KeyRoots[i])
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
