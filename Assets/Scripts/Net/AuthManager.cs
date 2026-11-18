using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

namespace ScrapFishing.Net
{
    // 네트워크 프로그래밍 10강 실습 5: Firebase 익명(게스트) 로그인. Login 씬에 둔다.
    // 첫 로그인은 accounts:signUp으로 새 uid를 받고, 다음부터는 저장해 둔 refreshToken으로 같은 uid의 새 idToken을 받는다.
    // Uid·IdToken·Name은 static이라 씬이 바뀌어도(Menu·Boat) 그대로 남는다.
    // (강의는 TMP_Text지만 이 프로젝트는 TextMeshPro가 없고 한글 픽셀 폰트를 legacy Text로 쓰므로 Text를 쓴다.)
    public class AuthManager : MonoBehaviour
    {
        public string apiKey;
        public Text statusText;                                    // Login 화면 상태 표시
        public static string Uid = "", IdToken = "", Name = "";   // static: 씬이 바뀌어도 유지

        bool busy;                                                 // 버튼 연타로 요청이 겹치지 않게

        public void GuestLogin()
        {
            if (busy) return;
            busy = true;
            statusText.text = "로그인 중...";
            StartCoroutine(SignIn());
        }

        public static void Logout() { Uid = ""; IdToken = ""; Name = ""; SceneManager.LoadScene("Login"); }

        IEnumerator SignIn()
        {
            string saved = PlayerPrefs.GetString("refreshToken", "");   // 저장된 토큰이 있으면 같은 계정으로 다시 로그인
            string signUp = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + apiKey;
            string refresh = "https://securetoken.googleapis.com/v1/token?key=" + apiKey;
            UnityWebRequest req = saved == ""   // 처음: 새 익명 계정 / 다음: 토큰 갱신
                ? UnityWebRequest.Post(signUp, "{\"returnSecureToken\":true}", "application/json")
                : UnityWebRequest.Post(refresh, "grant_type=refresh_token&refresh_token=" + saved, "application/x-www-form-urlencoded");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                PlayerPrefs.DeleteKey("refreshToken");   // 망가진 토큰이면 지우고 다음에 새 계정으로
                statusText.text = req.error;
                Debug.LogWarning("login failed " + req.error + "\n" + req.downloadHandler.text);
                req.Dispose();
                busy = false;
                yield break;
            }

            AuthResponse r = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
            req.Dispose();
            Uid = saved == "" ? r.localId : r.user_id; IdToken = saved == "" ? r.idToken : r.id_token;
            PlayerPrefs.SetString("refreshToken", saved == "" ? r.refreshToken : r.refresh_token); PlayerPrefs.Save();
            Name = "게스트 " + Uid.Substring(0, 6);
            Debug.Log("login ok uid=" + Uid);
            SceneManager.LoadScene("Menu");   // 로그인 성공하면 Menu로
        }
    }

    // signUp 응답(idToken, localId, refreshToken)과 token 갱신 응답(id_token, user_id, refresh_token)을 한 클래스로 받는다.
    [System.Serializable] public class AuthResponse { public string idToken, localId, refreshToken, id_token, user_id, refresh_token; }
}
