using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // 저장 경로를 PlayerPrefs(WebGL에서는 IndexedDB)로 통일하되, 테스트에서는 메모리로 바꿔 끼운다.
    public interface IKeyValueStore
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public static readonly PlayerPrefsStore Instance = new PlayerPrefsStore();

        PlayerPrefsStore()
        {
        }

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class MemoryKeyValueStore : IKeyValueStore
    {
        readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
        readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

        public int SaveCount { get; private set; }

        public bool HasKey(string key) => _ints.ContainsKey(key) || _strings.ContainsKey(key);
        public int GetInt(string key, int fallback) => _ints.TryGetValue(key, out var value) ? value : fallback;
        public string GetString(string key, string fallback) => _strings.TryGetValue(key, out var value) ? value : fallback;

        public void SetInt(string key, int value)
        {
            _strings.Remove(key);
            _ints[key] = value;
        }

        public void SetString(string key, string value)
        {
            _ints.Remove(key);
            _strings[key] = value;
        }

        public void DeleteKey(string key)
        {
            _ints.Remove(key);
            _strings.Remove(key);
        }

        public void Save()
        {
            SaveCount++;
        }
    }
}
