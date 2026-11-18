using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace ScrapFishing.Net
{
    // 네트워크 프로그래밍 10강 실습 7: 최고 기록을 Realtime Database /scores/{uid}에 PUT 한다. Boat 씬에 둔다.
    // 서버 기록을 먼저 읽고(GET) 새 점수가 더 높을 때만 쓴다. 규칙(실습 6)이 auth.uid == $uid만 쓰기 허용하므로 ?auth=idToken을 붙인다.
    public class ScoreSaver : MonoBehaviour
    {
        public string dbUrl;                    // Realtime Database 주소. 끝에 / 없이
        public static ScoreSaver Instance;      // 어디서나 ScoreSaver.Instance로 호출
        bool busy;                              // 저장 중에 또 부르면 무시

        void Awake() { Instance = this; }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Save(int score)
        {
            if (AuthManager.Uid == "" || busy) return;   // 로그인 안 했거나 저장 중이면 건너뜀
            busy = true; StartCoroutine(Put(score));
        }

        IEnumerator Put(int score)
        {
            string url = dbUrl + "/scores/" + AuthManager.Uid + ".json?auth=" + AuthManager.IdToken;
            UnityWebRequest get = UnityWebRequest.Get(url);
            yield return get.SendWebRequest();           // 서버 최고 기록 읽기. 없으면 null
            string old = get.downloadHandler.text;
            get.Dispose();
            int best = old.StartsWith("{") ? JsonUtility.FromJson<ScoreData>(old).score : 0;
            if (score <= best) { busy = false; yield break; }   // 서버 기록이 같거나 높으면 안 씀

            string body = "{\"name\":\"" + AuthManager.Name + "\",\"score\":" + score + "}";
            UnityWebRequest req = UnityWebRequest.Put(url, body);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            Debug.Log(req.result == UnityWebRequest.Result.Success ? "saved" : "save failed " + req.error);
            req.Dispose();
            busy = false;
        }
    }

    [System.Serializable] public class ScoreData { public string name; public int score; }
}
