using System;
using CrazyGames;
using UnityEditor;

namespace ScrapFishing.Build
{
    // CrazyGames SDK 사이트락은 SDK를 초기화하지 않아도 모든 WebGL 빌드에서 게임 시작 전에 돈다
    // (CrazySDK.OnRuntimeMethodLoad → SiteLock.Check). 허용 목록(CrazyGamesSettings.whitelistedDomains)에 없는 도메인이면
    // 게임을 멈추므로, 배포할 도메인이 들어 있는지 빌드 전과 테스트에서 확인한다. SDK를 다시 가져오면 목록이 비워진다.
    public static class GitHubPagesSiteLock
    {
        public const string Domain = "https://jungys24.github.io";
        public const string SettingsPath = "Assets/CrazySDK/Resources/CrazyGamesSettings.asset";

        public static bool IsWhitelisted(out string problem)
        {
            var host = new Uri(Domain).Host;
            if (FindHost(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase), out _, out problem))
            {
                return true;
            }

            if (problem == null)
            {
                problem = SettingsPath + "의 whitelistedDomains에 " + Domain + "이 없습니다. SDK를 다시 가져오면 지워지니 다시 넣어 주세요"
                    + " (없으면 CrazyGames SDK 사이트락이 GitHub Pages 판을 멈춥니다).";
            }

            return false;
        }

        // 허용 목록에서 match를 만족하는 첫 호스트를 찾는다. 설정 파일이 없으면 problem에 이유를 적는다(못 찾으면 null).
        public static bool FindHost(Func<string, bool> match, out string host, out string problem)
        {
            host = null;
            var settings = AssetDatabase.LoadAssetAtPath<CrazySettings>(SettingsPath);
            if (settings == null)
            {
                problem = "CrazyGames SDK 설정이 없습니다: " + SettingsPath;
                return false;
            }

            problem = null;
            if (settings.whitelistedDomains == null)
            {
                return false;
            }

            foreach (var entry in settings.whitelistedDomains)
            {
                var candidate = HostOf(entry);
                if (candidate != null && match(candidate))
                {
                    host = candidate;
                    return true;
                }
            }

            return false;
        }

        // SDK와 같은 규칙: 스킴이 없으면 http://를 붙여 호스트만 본다(SiteLock.IsOnWhitelistedDomain).
        public static string HostOf(string entry)
        {
            var value = (entry ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return null;
            }

            if (!value.StartsWith("http://") && !value.StartsWith("https://"))
            {
                value = "http://" + value;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : null;
        }
    }
}
