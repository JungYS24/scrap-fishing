using System;
using System.Collections.Generic;
using System.Linq;
using CrazyGames;
using UnityEditor;

namespace ScrapFishing.Build
{
    // CrazyGames SDK 사이트락은 SDK를 초기화하지 않아도 모든 WebGL 빌드에서 게임 시작 전에 돈다
    // (CrazySDK.OnRuntimeMethodLoad → SiteLock.Check). 허용 목록(CrazyGamesSettings.whitelistedDomains)에 없는 도메인이면
    // 게임을 멈추므로, GitHub Pages(·itch.io) 도메인이 들어 있는지 빌드 전과 테스트에서 확인한다. SDK를 다시 가져오면 목록이 비워진다.
    public static class GitHubPagesSiteLock
    {
        public const string Domain = "https://jungys24.github.io";
        public const string SettingsPath = "Assets/CrazySDK/Resources/CrazyGamesSettings.asset";

        public static bool IsWhitelisted(out string problem)
        {
            return AreWhitelisted(new[] { Domain }, "GitHub Pages", out problem);
        }

        // domains가 모두 허용 목록에 있는지. 빠진 것이 있으면 problem에 빠진 도메인을 적는다(target은 멈출 판 이름).
        public static bool AreWhitelisted(IEnumerable<string> domains, string target, out string problem)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CrazySettings>(SettingsPath);
            if (settings == null)
            {
                problem = "CrazyGames SDK 설정이 없습니다: " + SettingsPath;
                return false;
            }

            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (settings.whitelistedDomains != null)
            {
                foreach (var entry in settings.whitelistedDomains)
                {
                    var value = (entry ?? string.Empty).Trim();
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    // SDK와 같은 규칙: 스킴이 없으면 http://를 붙여 호스트만 비교한다(SiteLock.IsOnWhitelistedDomain).
                    if (!value.StartsWith("http://") && !value.StartsWith("https://"))
                    {
                        value = "http://" + value;
                    }

                    if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    {
                        hosts.Add(uri.Host);
                    }
                }
            }

            var missing = domains.Where(d => !hosts.Contains(new Uri(d).Host)).ToArray();
            if (missing.Length == 0)
            {
                problem = null;
                return true;
            }

            problem = SettingsPath + "의 whitelistedDomains에 " + string.Join(", ", missing) + "이 없습니다. SDK를 다시 가져오면 지워지니 다시 넣어 주세요"
                + " (없으면 CrazyGames SDK 사이트락이 " + target + " 판을 멈춥니다).";
            return false;
        }
    }
}
