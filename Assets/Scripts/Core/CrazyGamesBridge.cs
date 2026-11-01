using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
using CrazyGames;
#endif

namespace ScrapFishing.Core
{
    // CrazyGames SDK(Assets/CrazySDK) 호출을 한곳에 모은다. CRAZYGAMES 웹 빌드에서만 SDK를 부르고,
    // 에디터·GitHub Pages·다른 플랫폼에서는 아무 일도 하지 않는다(CRAZYGAMES 에디터에서는 무엇을 보냈을지 로그만 남긴다).
    // 플레이 중 여부는 게임이 매 프레임 SetGameplay로 알려 주고, 탭이 백그라운드로 가거나 광고 중이면 멈춤으로 본다.
    // 바뀔 때만 GameplayStart/GameplayStop을 보낸다. SDK 초기화가 끝나기 전 상태는 끝난 뒤 한 번에 보낸다.
    // 로딩 시작·끝(loadingStart/Stop)은 SDK 문서상 Unity에서 지원하지 않는다(Unity 로더가 맡는다).
    public class CrazyGamesBridge : MonoBehaviour
    {
        static CrazyGamesBridge _instance;
        static bool _ready;
        static bool _wantPlay;
        static bool _focused = true;
        static bool _inAd;
        static bool _sent;
        static bool _sentPlay;
        static bool _audioPausedBeforeAd;
        static EventSystem _blockedEvents;

        // 광고가 시작·끝날 때. 소리·입력은 여기서 막고 풀지만, 화면별로 더 할 일이 있으면 구독한다.
        public static event Action<bool> AdBreakChanged;

        public static bool InAdBreak => _inAd;

        // 마지막으로 보낸(에디터에서는 보냈을) 신호. true=GameplayStart, false=GameplayStop, null=아직 없음.
        public static bool? LastSent => _sent ? _sentPlay : (bool?)null;

        // 실제로 SDK를 부르는 빌드인지.
        public static bool Active
        {
            get
            {
#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
                return CrazySDK.IsAvailable;
#else
                return false;
#endif
            }
        }

        // 첫 씬에서 한 번 부른다(씬을 다시 불러도 한 번만 초기화한다).
        public static void Init()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("CrazyGamesBridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CrazyGamesBridge>();
#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
            if (CrazySDK.IsAvailable)
            {
                CrazySDK.Init(() =>
                {
                    _ready = true;
                    Flush();
                });
            }
#else
            EditorLog("Init (SDK 호출 안 함)");
#endif
        }

        // 게임이 지금 플레이 중인지(출항해서 낚시·잠수 중이고 설정·오리파 창이 닫혀 있음).
        public static void SetGameplay(bool playing)
        {
            if (_wantPlay == playing)
            {
                return;
            }

            _wantPlay = playing;
            Flush();
        }

        // TODO(광고): 광고를 넣기로 하면 쉬는 지점(예: 결과 화면이 뜬 뒤, 다시 출항하기 전)에서 부른다.
        // Basic Launch에서는 광고가 꺼져 있어 SDK가 바로 오류로 돌려준다. 끝나면(성공·실패 모두) done을 부른다.
        public static void RequestMidgameAd(Action done)
        {
#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
            if (Active && _ready)
            {
                CrazySDK.Ad.RequestAd(
                    CrazyAdType.Midgame,
                    BeginAdBreak,
                    _ =>
                    {
                        EndAdBreak();
                        done?.Invoke();
                    },
                    () =>
                    {
                        EndAdBreak();
                        done?.Invoke();
                    });
                return;
            }
#endif
            done?.Invoke();
        }

        // 광고가 나오는 동안 소리를 끄고 입력(UI·탭·스와이프·조이스틱)을 막는다.
        public static void BeginAdBreak()
        {
            if (_inAd)
            {
                return;
            }

            _inAd = true;
            _audioPausedBeforeAd = AudioListener.pause;
            AudioListener.pause = true;
            _blockedEvents = EventSystem.current;
            if (_blockedEvents != null)
            {
                _blockedEvents.enabled = false;
            }

            AdBreakChanged?.Invoke(true);
            Flush();
        }

        public static void EndAdBreak()
        {
            if (!_inAd)
            {
                return;
            }

            _inAd = false;
            AudioListener.pause = _audioPausedBeforeAd;
            if (_blockedEvents != null)
            {
                _blockedEvents.enabled = true;
                _blockedEvents = null;
            }

            AdBreakChanged?.Invoke(false);
            Flush();
        }

        // 웹에서는 탭을 바꾸거나 창이 포커스를 잃으면 false가 온다.
        void OnApplicationFocus(bool focus)
        {
            _focused = focus;
            Flush();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        static void Flush()
        {
            var play = _wantPlay && _focused && !_inAd;
            // 한 번도 시작하지 않았으면 멈춤을 보낼 필요가 없다.
            if (_sent ? play == _sentPlay : !play)
            {
                return;
            }

#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
            if (!Active || !_ready)
            {
                return;
            }

            if (play)
            {
                CrazySDK.Game.GameplayStart();
            }
            else
            {
                CrazySDK.Game.GameplayStop();
            }
#else
            EditorLog(play ? "GameplayStart" : "GameplayStop");
#endif
            _sent = true;
            _sentPlay = play;
        }

        static void EditorLog(string message)
        {
#if CRAZYGAMES && UNITY_EDITOR
            Debug.Log("[CrazyGames] " + message);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _ready = false;
            _wantPlay = false;
            _focused = true;
            _inAd = false;
            _sent = false;
            _sentPlay = false;
            _blockedEvents = null;
            AdBreakChanged = null;
        }
    }
}
