using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrapFishing.Core
{
    // CrazyGames 빌드의 첫 씬(Assets/Scenes/CrazyGamesBoot.unity). 저장(Data 모듈)은 SDK 초기화가 끝나야 읽을 수 있으므로,
    // 여기서 SDK 초기화를 기다렸다가 게임 씬을 연다. 이 씬은 저장을 읽는 것이 없다. GitHub Pages 빌드에는 넣지 않는다.
    // SDK 문서도 초기화를 RuntimeInitializeOnLoadMethod가 아닌 별도 로딩 씬에서 하라고 권한다.
    public class CrazyGamesBoot : MonoBehaviour
    {
        public const string GameScene = "Boat";

        void Start()
        {
            CrazyGamesBridge.Init(() => SceneManager.LoadScene(GameScene));
        }
    }
}
