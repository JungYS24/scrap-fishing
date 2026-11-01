using System;
using System.Collections.Generic;
using ScrapFishing.Core;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.Tests
{
    // 테스트 동안 게임 저장(SaveStore)을 에디터 PlayerPrefs 대신 메모리로 돌린다.
    // 플레이 모드에 들어가면 도메인이 다시 불러와져 static이 사라지므로, 값은 SessionState(에디터 세션 메모리)에 두고
    // 도메인이 다시 불러와질 때마다([InitializeOnLoad]) 같은 저장소를 다시 끼운다. 에디터를 닫으면 사라진다.
    [InitializeOnLoad]
    public static class TestSaves
    {
        const string ActiveKey = "ScrapFishing.Tests.SaveStore.Active";
        const string DataKey = "ScrapFishing.Tests.SaveStore.Data";

        static TestSaves()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                SaveStore.Use(new SessionSaveStore());
            }
        }

        public static bool Active => SessionState.GetBool(ActiveKey, false);

        public static void Begin()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.EraseString(DataKey);
            SaveStore.Use(new SessionSaveStore());
        }

        public static void End()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseString(DataKey);
            SaveStore.Use(null);
        }

        [Serializable]
        class Snapshot
        {
            public List<string> keys = new List<string>();
            public List<string> values = new List<string>();
        }

        // MemorySaveStore와 같은 규칙(값은 문자열)으로, 바뀔 때마다 SessionState에 적어 둔다.
        sealed class SessionSaveStore : ISaveStore
        {
            readonly MemorySaveStore _memory = new MemorySaveStore();

            public SessionSaveStore()
            {
                var json = SessionState.GetString(DataKey, string.Empty);
                if (json.Length == 0)
                {
                    return;
                }

                var snapshot = JsonUtility.FromJson<Snapshot>(json);
                for (var i = 0; i < snapshot.keys.Count; i++)
                {
                    _memory.SetString(snapshot.keys[i], snapshot.values[i]);
                }
            }

            public bool HasKey(string key) => _memory.HasKey(key);
            public int GetInt(string key, int fallback) => _memory.GetInt(key, fallback);
            public float GetFloat(string key, float fallback) => _memory.GetFloat(key, fallback);
            public string GetString(string key, string fallback) => _memory.GetString(key, fallback);

            public void SetInt(string key, int value)
            {
                _memory.SetInt(key, value);
                Persist();
            }

            public void SetFloat(string key, float value)
            {
                _memory.SetFloat(key, value);
                Persist();
            }

            public void SetString(string key, string value)
            {
                _memory.SetString(key, value);
                Persist();
            }

            public void DeleteKey(string key)
            {
                _memory.DeleteKey(key);
                Persist();
            }

            public void DeleteAll()
            {
                _memory.DeleteAll();
                Persist();
            }

            public void Save()
            {
            }

            void Persist()
            {
                var snapshot = new Snapshot();
                foreach (var entry in _memory.Entries)
                {
                    snapshot.keys.Add(entry.Key);
                    snapshot.values.Add(entry.Value);
                }

                SessionState.SetString(DataKey, JsonUtility.ToJson(snapshot));
            }
        }
    }
}
