using System;
using ScrapFishing.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrapFishing.Core
{
    // 쿠폰 "reset": 저장 데이터를 모두 지우고 사이트에 처음 들어온 상태로 다시 시작한다(튜토리얼 확인용).
    // 저장은 전부 PlayerPrefs(WebGL은 IndexedDB)라 DeleteAll 한 번으로 달러·강화·장비·오리파 기록·설정·튜토리얼 진행이 모두 지워진다.
    // 씬을 넘어 살아 있는 값(음량 캐시, AudioManager의 음소거 적용)만 다시 읽고, 현재 씬을 새로 불러온다.
    public static class SaveReset
    {
        public const string Code = "reset";

        public static bool IsResetCode(string code)
        {
            return string.Equals((code ?? string.Empty).Trim(), Code, StringComparison.OrdinalIgnoreCase);
        }

        // wipe: 테스트에서 실제 저장을 지우지 않게 바꿔 끼운다. reload가 false면 씬은 그대로 둔다.
        public static void ResetAll(Action wipe = null, bool reload = true)
        {
            if (wipe != null)
            {
                wipe();
            }
            else
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }

            VolumeSettings.Reload();
            Loc.Reload();
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ReloadPrefs();
            }

            if (!reload)
            {
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
