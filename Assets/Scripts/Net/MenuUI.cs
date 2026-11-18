using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ScrapFishing.Net
{
    // 네트워크 프로그래밍 10강 실습 5: Menu 씬. 로그인한 이름을 보여 주고, 게임 시작(Boat 씬)과 로그아웃을 한다.
    public class MenuUI : MonoBehaviour
    {
        public Text uidText;

        void Start() { uidText.text = AuthManager.Name; }

        public void StartGame() { SceneManager.LoadScene("Boat"); }   // 기존 게임 씬

        public void Logout() { AuthManager.Logout(); }                 // static 호출
    }
}
