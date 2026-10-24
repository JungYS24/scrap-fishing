using System;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Controls
{
    public class TapReader : MonoBehaviour
    {
        // 에디터 디버그용 [Tap] 로그. 결과창 문제를 확인한 뒤 false로 끈다.
        public static bool LogTaps = true;

        [SerializeField] float slopPixels = 40f;

        public event Action OnTap;

        GameFlow _flow;
        Vector2 _pressPosition;
        bool _pressed;

        void Awake()
        {
            _flow = GetComponent<GameFlow>();
        }

        void Update()
        {
            if (!PointerUtil.TryRead(out var position, out var pressedThisFrame, out var releasedThisFrame))
            {
                return;
            }

            if (pressedThisFrame)
            {
                var inGame = FixedResolution.ContainsWindowPoint(position);
                var overUi = PointerUtil.IsOverUi(position);
#if UNITY_EDITOR
                Log("press", position, inGame, overUi, false);
#endif
                if (!inGame || overUi)
                {
                    return;
                }

                _pressed = true;
                _pressPosition = position;
            }

            if (!_pressed || !releasedThisFrame)
            {
                return;
            }

            _pressed = false;
            var blocked = SteerReader.BlocksTapAndSwipe;
            var slopOk = Vector2.Distance(FixedResolution.ToGamePixels(_pressPosition), FixedResolution.ToGamePixels(position)) <= slopPixels;
            var fired = !blocked && slopOk;
#if UNITY_EDITOR
            Log("release", position, FixedResolution.ContainsWindowPoint(position), PointerUtil.IsOverUi(position), fired);
#endif
            if (fired)
            {
                OnTap?.Invoke();
            }
        }

#if UNITY_EDITOR
        void Log(string stage, Vector2 position, bool inGame, bool overUi, bool fired)
        {
            if (!LogTaps)
            {
                return;
            }

            var phase = _flow != null ? _flow.Phase.ToString() : "?";
            Debug.Log(
                "[Tap] " + stage
                + " phase=" + phase
                + " inGame=" + inGame
                + " overUi=" + overUi
                + " hits=[" + PointerUtil.HitNames(position) + "]"
                + " blockSteer=" + SteerReader.BlocksTapAndSwipe
                + " onTap=" + fired);
        }
#endif
    }
}
