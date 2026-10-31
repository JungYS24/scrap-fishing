using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace ScrapFishing.Core
{
    public enum GameLanguage
    {
        Ko,
        En
    }

    // 한국어/영어 문자열 표. Resources/Localization/loc_master.csv(key,ko,en)를 처음 쓸 때 한 번 읽는다.
    // 원본은 scrap_fishing_loc_master.xlsx이고, 엑셀에서 CSV(UTF-8)로 저장해 이 파일을 덮어쓴다.
    // 언어는 PlayerPrefs "Language"(ko/en)에 저장하고, 처음 실행이면 기기 언어가 한국어일 때만 ko다.
    // 웹에서는 브라우저 언어(navigator.language)가 "ko"로 시작할 때만 ko이고, 그 밖의 언어나 읽기 실패는 en이다.
    public static class Loc
    {
        public const string Resource = "Localization/loc_master";
        public const string PrefKey = "Language";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern string ScrapFishing_BrowserLanguage();
#endif

        static Dictionary<string, string[]> _table;
        static readonly HashSet<string> Warned = new HashSet<string>();
        static GameLanguage _language;
        static bool _languageLoaded;

        // 언어가 바뀌면 열려 있는 화면이 글자를 다시 넣는다.
        public static event Action Changed;

        public static GameLanguage Language
        {
            get
            {
                EnsureLanguage();
                return _language;
            }
            set => SetLanguage(value, true);
        }

        // save가 false면 저장하지 않고 이번 실행에서만 바꾼다(테스트가 한국어로 고정할 때).
        public static void SetLanguage(GameLanguage value, bool save)
        {
            EnsureLanguage();
            if (save)
            {
                PlayerPrefs.SetString(PrefKey, Code(value));
                PlayerPrefs.Save();
            }

            if (_language == value)
            {
                return;
            }

            _language = value;
            Changed?.Invoke();
        }

        public static bool IsKorean => Language == GameLanguage.Ko;

        public static void Toggle()
        {
            Language = IsKorean ? GameLanguage.En : GameLanguage.Ko;
        }

        public static string T(string key)
        {
            return TryGet(key, out var text) ? text : Missing(key);
        }

        public static string F(string key, params object[] args)
        {
            var format = T(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        // 키가 없으면 fallback(보통 JSON의 기존 값)을 쓴다. 데이터 이름처럼 시트가 늦게 따라오는 곳용.
        public static string Or(string key, string fallback)
        {
            return TryGet(key, out var text) ? text : fallback;
        }

        public static bool Has(string key)
        {
            EnsureTable();
            return key != null && _table.ContainsKey(key);
        }

        public static bool TryGet(string key, out string text)
        {
            EnsureTable();
            text = null;
            if (key == null || !_table.TryGetValue(key, out var row))
            {
                return false;
            }

            text = row[(int)Language];
            if (string.IsNullOrEmpty(text))
            {
                // 영어 칸이 비어 있으면 한국어라도 보여 준다.
                text = row[(int)GameLanguage.Ko];
            }

            return true;
        }

        public static string Ko(string key)
        {
            EnsureTable();
            return key != null && _table.TryGetValue(key, out var row) ? row[0] : null;
        }

        public static string En(string key)
        {
            EnsureTable();
            return key != null && _table.TryGetValue(key, out var row) ? row[1] : null;
        }

        public static IEnumerable<string> Keys
        {
            get
            {
                EnsureTable();
                return _table.Keys;
            }
        }

        // 저장 초기화 뒤 PlayerPrefs가 비었으면 기기 언어로 돌아간다.
        public static void Reload()
        {
            var before = _languageLoaded ? _language : (GameLanguage?)null;
            _languageLoaded = false;
            EnsureLanguage();
            if (before.HasValue && before.Value != _language)
            {
                Changed?.Invoke();
            }
        }

        // 테스트용: 표를 직접 넣는다. null이면 다음에 Resources에서 다시 읽는다.
        public static void LoadTable(string csv)
        {
            _table = csv == null ? null : Build(csv);
            Warned.Clear();
        }

        public static string Code(GameLanguage language)
        {
            return language == GameLanguage.En ? "en" : "ko";
        }

        static void EnsureLanguage()
        {
            if (_languageLoaded)
            {
                return;
            }

            _languageLoaded = true;
            var saved = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (saved == "ko")
            {
                _language = GameLanguage.Ko;
            }
            else if (saved == "en")
            {
                _language = GameLanguage.En;
            }
            else
            {
                _language = DeviceLanguage();
            }
        }

        // "ko", "ko-KR"처럼 ko로 시작하면 한국어, 나머지(빈 값 포함)는 영어.
        public static GameLanguage FromLanguageTag(string tag)
        {
            return (tag ?? string.Empty).Trim().StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? GameLanguage.Ko : GameLanguage.En;
        }

        static GameLanguage DeviceLanguage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                return FromLanguageTag(ScrapFishing_BrowserLanguage());
            }
            catch (Exception)
            {
                return GameLanguage.En;
            }
#else
            return Application.systemLanguage == SystemLanguage.Korean ? GameLanguage.Ko : GameLanguage.En;
#endif
        }

        static void EnsureTable()
        {
            if (_table != null)
            {
                return;
            }

            var asset = Resources.Load<TextAsset>(Resource);
            _table = asset != null ? Build(asset.text) : new Dictionary<string, string[]>();
            if (asset == null)
            {
                Debug.LogWarning("번역 표가 없습니다: Resources/" + Resource + ".csv");
            }
        }

        static Dictionary<string, string[]> Build(string csv)
        {
            var table = new Dictionary<string, string[]>();
            var rows = ParseCsv(csv);
            if (rows.Count == 0)
            {
                return table;
            }

            // 첫 줄은 머리글. 열 순서가 바뀌어도 key/ko/en 이름으로 찾는다.
            var header = rows[0];
            var keyCol = Math.Max(0, header.IndexOf("key"));
            var koCol = header.IndexOf("ko");
            var enCol = header.IndexOf("en");
            if (koCol < 0) koCol = 1;
            if (enCol < 0) enCol = 2;
            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var key = Cell(row, keyCol).Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                table[key] = new[] { Cell(row, koCol), Cell(row, enCol) };
            }

            return table;
        }

        static string Cell(List<string> row, int index)
        {
            return index >= 0 && index < row.Count ? row[index] : string.Empty;
        }

        // RFC 4180: 쉼표·줄바꿈·따옴표가 든 칸은 큰따옴표로 감싸고, 안의 따옴표는 두 번 쓴다. BOM과 \r\n도 받는다.
        public static List<List<string>> ParseCsv(string csv)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(csv))
            {
                return rows;
            }

            var start = csv[0] == '﻿' ? 1 : 0;
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            var rowHasData = false;
            for (var i = start; i < csv.Length; i++)
            {
                var c = csv[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else if (c != '\r')
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        rowHasData = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Length = 0;
                        rowHasData = true;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        if (rowHasData || cell.Length > 0)
                        {
                            row.Add(cell.ToString());
                            rows.Add(row);
                        }

                        row = new List<string>();
                        cell.Length = 0;
                        rowHasData = false;
                        break;
                    default:
                        cell.Append(c);
                        rowHasData = true;
                        break;
                }
            }

            if (rowHasData || cell.Length > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        static string Missing(string key)
        {
            if (Debug.isDebugBuild && key != null && Warned.Add(key))
            {
                Debug.LogWarning("번역 키가 없습니다: " + key);
            }

            return key ?? string.Empty;
        }
    }
}
