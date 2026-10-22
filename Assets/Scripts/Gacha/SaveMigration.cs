using System;
using ScrapFishing.Core;
using ScrapFishing.Fishing;

namespace ScrapFishing.Gacha
{
    public static class SaveMigration
    {
        public const string VersionKey = "SaveVersion";
        public const int CurrentVersion = 3;

        // 반환값: 저장 데이터를 바꿨으면 true.
        // v2 → v3: 달러·강화 레벨을 그대로 두고 가챠 저장만 추가한다.
        // v2 미만: 예전 규칙대로 달러·강화를 초기화한다(밸런스가 바뀌기 전의 저장값).
        // v3 이상: 건드리지 않는다. 더 높은 버전을 낮은 빌드가 지워 버리는 일을 막는다.
        public static bool Run(IKeyValueStore store)
        {
            var version = store.GetInt(VersionKey, 0);
            if (version >= CurrentVersion)
            {
                return false;
            }

            if (version < 2)
            {
                store.DeleteKey(Progression.DollarKey);
                store.DeleteKey(RodStats.LevelKey);
                foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)))
                {
                    store.DeleteKey(Progression.LevelKey(kind));
                }
            }

            EnsureGachaSave(store);
            store.SetInt(VersionKey, CurrentVersion);
            store.Save();
            return true;
        }

        // 가챠 저장이 없거나 깨졌으면 빈 데이터로 새로 만든다. 실패해도 다른 키는 그대로 둔다.
        static void EnsureGachaSave(IKeyValueStore store)
        {
            try
            {
                var data = GearInventory.Read(store);
                store.SetString(GearInventory.SaveKey, UnityEngine.JsonUtility.ToJson(data));
            }
            catch (Exception)
            {
                store.DeleteKey(GearInventory.SaveKey);
            }
        }
    }
}
