using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
using CrazyGames;
#endif

namespace ScrapFishing.Core
{
    // 게임 저장(달러·강화·장비·오리파 기록·설정·튜토리얼)을 읽고 쓰는 곳. 키 이름과 값 형식은 PlayerPrefs 시절 그대로다.
    public interface ISaveStore
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        // 저장 초기화(reset 쿠폰)에서만 쓴다.
        void DeleteAll();
        void Save();
    }

    // 지금 쓰는 저장소. GitHub Pages·에디터는 PlayerPrefs(WebGL은 IndexedDB),
    // CrazyGames 웹 빌드는 CrazyGames SDK Data 모듈(로그인하면 계정에 동기화)이다. 테스트는 Use로 바꿔 끼운다.
    // CrazyGames 빌드에서는 SDK 초기화가 끝난 뒤에만 읽어야 한다(CrazyGamesBoot 씬이 초기화를 기다렸다가 게임 씬을 연다).
    public static class SaveStore
    {
        static Tracked _current;

        // 실제 저장소를 감싸 이번 실행에서 읽고 쓴 키의 마지막 값을 기억한다(디버그 크기 계산용).
        // PlayerPrefs는 키 목록을 줄 수 없고, 가챠 저장처럼 Current를 직접 받아 쓰는 곳도 있어 여기서 모은다.
        public static ISaveStore Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new Tracked(CreateDefault());
                }

                return _current;
            }
        }

        // null이면 빌드 기본 저장소로 돌아간다.
        public static void Use(ISaveStore store)
        {
            _current = store != null ? new Tracked(store is Tracked tracked ? tracked.Inner : store) : null;
        }

        public static bool HasKey(string key) => Current.HasKey(key);
        public static int GetInt(string key, int fallback = 0) => Current.GetInt(key, fallback);
        public static void SetInt(string key, int value) => Current.SetInt(key, value);
        public static float GetFloat(string key, float fallback = 0f) => Current.GetFloat(key, fallback);
        public static void SetFloat(string key, float value) => Current.SetFloat(key, value);
        public static string GetString(string key, string fallback = "") => Current.GetString(key, fallback);
        public static void SetString(string key, string value) => Current.SetString(key, value);
        public static void DeleteKey(string key) => Current.DeleteKey(key);
        public static void DeleteAll() => Current.DeleteAll();
        public static void Save() => Current.Save();

        // 디버그: 저장 내용을 CrazyGames Data 모듈처럼 하나의 JSON 객체(문자열 값)로 바꿨을 때의 바이트 수. 한도는 1MB.
        public const int DataLimitBytes = 1048576;

        // 디버그: 이번 실행에서 읽고 쓴 저장 전체의 크기(바이트). 게임을 한 판 하고 오리파·보관함을 열어 보면 거의 모든 키가 잡힌다.
        public static int DebugEstimateBytes()
        {
            return EstimateJsonBytes(((Tracked)Current).Seen);
        }

        public static int EstimateJsonBytes(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var json = new StringBuilder("{");
            var first = true;
            foreach (var entry in entries)
            {
                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                AppendJsonString(json, entry.Key);
                json.Append(':');
                AppendJsonString(json, entry.Value);
            }

            json.Append('}');
            return Encoding.UTF8.GetByteCount(json.ToString());
        }

        static void AppendJsonString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var c in value ?? string.Empty)
            {
                if (c == '"' || c == '\\')
                {
                    json.Append('\\');
                }

                json.Append(c);
            }

            json.Append('"');
        }

        sealed class Tracked : ISaveStore
        {
            public readonly ISaveStore Inner;
            public readonly Dictionary<string, string> Seen = new Dictionary<string, string>();

            public Tracked(ISaveStore inner)
            {
                Inner = inner;
            }

            public bool HasKey(string key) => Inner.HasKey(key);

            public int GetInt(string key, int fallback)
            {
                var value = Inner.GetInt(key, fallback);
                if (value != fallback)
                {
                    Seen[key] = value.ToString(CultureInfo.InvariantCulture);
                }

                return value;
            }

            public void SetInt(string key, int value)
            {
                Inner.SetInt(key, value);
                Seen[key] = value.ToString(CultureInfo.InvariantCulture);
            }

            public float GetFloat(string key, float fallback)
            {
                var value = Inner.GetFloat(key, fallback);
                if (!Mathf.Approximately(value, fallback))
                {
                    Seen[key] = value.ToString(CultureInfo.InvariantCulture);
                }

                return value;
            }

            public void SetFloat(string key, float value)
            {
                Inner.SetFloat(key, value);
                Seen[key] = value.ToString(CultureInfo.InvariantCulture);
            }

            public string GetString(string key, string fallback)
            {
                var value = Inner.GetString(key, fallback);
                if (value != null && value != fallback)
                {
                    Seen[key] = value;
                }

                return value;
            }

            public void SetString(string key, string value)
            {
                Inner.SetString(key, value);
                if (value == null)
                {
                    Seen.Remove(key);
                }
                else
                {
                    Seen[key] = value;
                }
            }

            public void DeleteKey(string key)
            {
                Inner.DeleteKey(key);
                Seen.Remove(key);
            }

            public void DeleteAll()
            {
                Inner.DeleteAll();
                Seen.Clear();
            }

            public void Save() => Inner.Save();
        }

        static ISaveStore CreateDefault()
        {
#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
            if (CrazySDK.IsAvailable)
            {
                return new CrazyDataSaveStore();
            }
#endif
            return PlayerPrefsSaveStore.Instance;
        }
    }

    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public static readonly PlayerPrefsSaveStore Instance = new PlayerPrefsSaveStore();

        PlayerPrefsSaveStore()
        {
        }

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void DeleteAll() => PlayerPrefs.DeleteAll();
        public void Save() => PlayerPrefs.Save();
    }

#if CRAZYGAMES && UNITY_WEBGL && !UNITY_EDITOR
    // CrazyGames Data 모듈. 게스트는 브라우저 localStorage, 로그인하면 계정에 저장·동기화된다(최대 1MB).
    // SDK가 쓰기를 1~30초 묶어서 보내므로 따로 비우는 API가 없다. Save는 할 일이 없지만 호출 지점은 그대로 둔다.
    public sealed class CrazyDataSaveStore : ISaveStore
    {
        public bool HasKey(string key) => CrazySDK.Data.HasKey(key);
        public int GetInt(string key, int fallback) => CrazySDK.Data.GetInt(key, fallback);
        public void SetInt(string key, int value) => CrazySDK.Data.SetInt(key, value);
        public float GetFloat(string key, float fallback) => CrazySDK.Data.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => CrazySDK.Data.SetFloat(key, value);
        public string GetString(string key, string fallback) => CrazySDK.Data.GetString(key, fallback);
        public void SetString(string key, string value) => CrazySDK.Data.SetString(key, value);
        public void DeleteKey(string key) => CrazySDK.Data.DeleteKey(key);
        public void DeleteAll() => CrazySDK.Data.DeleteAll();

        public void Save()
        {
        }
    }
#endif

    // 테스트·용량 계산용 메모리 저장소. 값은 Data 모듈처럼 문자열로 들고 있다.
    public sealed class MemorySaveStore : ISaveStore
    {
        readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public int SaveCount { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> Entries => _values;

        public int EstimateJsonBytes() => SaveStore.EstimateJsonBytes(_values);

        public bool HasKey(string key) => _values.ContainsKey(key);

        public int GetInt(string key, int fallback)
        {
            return _values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        public void SetInt(string key, int value)
        {
            _values[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public float GetFloat(string key, float fallback)
        {
            return _values.TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        public void SetFloat(string key, float value)
        {
            _values[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public string GetString(string key, string fallback)
        {
            return _values.TryGetValue(key, out var value) ? value : fallback;
        }

        public void SetString(string key, string value)
        {
            if (value == null)
            {
                _values.Remove(key);
                return;
            }

            _values[key] = value;
        }

        public void DeleteKey(string key)
        {
            _values.Remove(key);
        }

        public void DeleteAll()
        {
            _values.Clear();
        }

        public void Save()
        {
            SaveCount++;
        }
    }
}
