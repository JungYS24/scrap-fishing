#if UNITY_EDITOR
using ScrapFishing.Data;
using ScrapFishing.Scrap;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    public static class RarityGlowPreview
    {
        const string DatabasePath = "Assets/Resources/ItemDatabase.asset";

        [MenuItem("Tools/Scrap Fishing/Preview Rarity Glow/Rare")]
        public static void Rare()
        {
            Spawn(ItemRarity.Rare, 0f);
        }

        [MenuItem("Tools/Scrap Fishing/Preview Rarity Glow/Epic")]
        public static void Epic()
        {
            Spawn(ItemRarity.Epic, 0f);
        }

        [MenuItem("Tools/Scrap Fishing/Preview Rarity Glow/Legend")]
        public static void Legend()
        {
            Spawn(ItemRarity.Legend, 0f);
        }

        [MenuItem("Tools/Scrap Fishing/Preview Rarity Glow/All")]
        public static void All()
        {
            Spawn(ItemRarity.Rare, -1.6f);
            Spawn(ItemRarity.Epic, 0f);
            Spawn(ItemRarity.Legend, 1.6f);
        }

        static void Spawn(ItemRarity rarity, float xOffset)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("플레이 모드에서만 등급 빛을 미리 볼 수 있습니다.");
                return;
            }

            var db = ItemDatabase.Instance;
            if (db == null || db.Items == null || db.Items.Count == 0)
            {
                db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
                if (db != null)
                {
                    ItemDatabase.BindEditorInstance(db);
                }
            }

            if (db == null)
            {
                Debug.LogError("ItemDatabase가 없습니다.");
                return;
            }

            var pool = db.GetByRarity(rarity);
            if (pool == null || pool.Count == 0)
            {
                Debug.LogError("해당 등급 아이템이 없습니다: " + rarity);
                return;
            }

            var item = pool[0];
            var camera = Camera.main;
            var position = camera != null
                ? camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(camera.transform.position.z)))
                : Vector3.zero;
            position.z = 0f;
            position.x += xOffset;

            var go = new GameObject("Preview " + item.Id);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.4f;
            var view = go.AddComponent<ScrapView>();
            view.Bind(item);
            if (item.HasAnimation && item.AnimatorController != null)
            {
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = item.AnimatorController;
            }
        }
    }
}
#endif
