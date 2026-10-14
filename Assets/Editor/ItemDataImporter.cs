using System;
using System.Collections.Generic;
using System.IO;
using ScrapFishing.Data;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    public static class ItemDataImporter
    {
        const string JsonPath = "Assets/Art/Fish/fish_items.json";
        const string ArtRoot = "Assets/Art/Fish/";
        const string ItemsFolder = "Assets/Data/Items";
        const string PrefabsFolder = "Assets/Prefabs/Items";
        const string DatabasePath = "Assets/Resources/ItemDatabase.asset";

        [MenuItem("Tools/Scrap Fishing/Import Items From JSON")]
        public static void ImportFromJson()
        {
            var json = LoadJson();
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("fish_items.json을 읽지 못했습니다: " + JsonPath);
                return;
            }

            var root = JsonUtility.FromJson<ItemJsonRoot>(PrepareJson(json));
            if (root == null || root.items == null || root.items.Count == 0)
            {
                root = FallbackRoot(json);
            }

            if (root == null || root.items == null || root.items.Count == 0)
            {
                Debug.LogError("fish_items.json 파싱 결과가 비어 있습니다.");
                return;
            }

            EnsureFolder(ItemsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");

            var created = new List<ItemData>();
            var animated = 0;
            var still = 0;
            var failed = 0;
            var sortingLayer = ResolveSortingLayer();

            for (var i = 0; i < root.items.Count; i++)
            {
                var item = root.items[i];
                if (item == null || string.IsNullOrEmpty(item.id))
                {
                    failed++;
                    Debug.LogError("아이템 id가 없습니다. index=" + i);
                    continue;
                }

                var pngPath = ArtPath(item.sprite);
                var icon = LoadIcon(pngPath, item.id);
                if (icon == null)
                {
                    failed++;
                    continue;
                }

                AnimationClip clip = null;
                RuntimeAnimatorController controller = null;
                if (item.hasAnimation)
                {
                    var anim = item.animation;
                    if (anim != null && !string.IsNullOrEmpty(anim.clip))
                    {
                        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtPath(anim.clip));
                        if (clip == null)
                        {
                            Debug.LogWarning("AnimationClip 로드 실패: " + item.id + " / " + ArtPath(anim.clip));
                        }
                    }

                    if (anim != null && !string.IsNullOrEmpty(anim.controller))
                    {
                        controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ArtPath(anim.controller));
                        if (controller == null)
                        {
                            Debug.LogWarning("AnimatorController 로드 실패: " + item.id + " / " + ArtPath(anim.controller));
                        }
                    }
                }

                var assetPath = ItemsFolder + "/" + item.id + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<ItemData>();
                    AssetDatabase.CreateAsset(data, assetPath);
                }

                data.ApplyImported(
                    item.id,
                    item.name,
                    ParseCategory(item.category),
                    ParseRarity(item.rarity),
                    item.zone,
                    item.chip,
                    item.gold,
                    icon,
                    item.hasAnimation,
                    clip,
                    controller);
                EditorUtility.SetDirty(data);

                var prefab = SaveItemPrefab(item.id, data, sortingLayer);
                data.SetPrefab(prefab);
                EditorUtility.SetDirty(data);

                created.Add(data);
                if (item.hasAnimation)
                {
                    animated++;
                }
                else
                {
                    still++;
                }
            }

            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.ReplaceItems(created);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"총 {created.Count}개 / 애니메이션 {animated}개 / 정지 {still}개 / 실패 {failed}개");
            ValidateItems();
        }

        [MenuItem("Tools/Scrap Fishing/Validate Items")]
        public static void ValidateItems()
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("ItemDatabase가 없습니다: " + DatabasePath);
                return;
            }

            var errors = 0;
            var items = database.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    errors++;
                    Debug.LogError("ItemDatabase items[" + i + "] 가 null 입니다.");
                    continue;
                }

                if (item.Icon == null)
                {
                    errors++;
                    Debug.LogError("icon null: " + item.Id);
                }

                if (item.HasAnimation && item.AnimatorController == null)
                {
                    errors++;
                    Debug.LogError("hasAnimation인데 controller null: " + item.Id);
                }

                if (item.Chip <= 0 || item.Gold <= 0)
                {
                    errors++;
                    Debug.LogError("chip/gold 0 이하: " + item.Id + " chip=" + item.Chip + " gold=" + item.Gold);
                }
            }

            if (errors == 0)
            {
                Debug.Log("Validate Items: 에러 0건 / " + items.Count + "개");
            }
            else
            {
                Debug.LogWarning("Validate Items: 에러 " + errors + "건");
            }
        }

        public static void ImportAndVerify()
        {
            ImportFromJson();
            var lobsterGuid = AssetDatabase.AssetPathToGUID(ItemsFolder + "/lobster.asset");
            ImportFromJson();
            var lobsterGuidAfter = AssetDatabase.AssetPathToGUID(ItemsFolder + "/lobster.asset");
            if (lobsterGuid != lobsterGuidAfter)
            {
                Debug.LogError("GUID 변경: lobster " + lobsterGuid + " -> " + lobsterGuidAfter);
            }
            else
            {
                Debug.Log("GUID 유지: lobster " + lobsterGuid);
            }

            LogItem("lobster");
            LogItem("shark_king");
            LogItem("seahorse");
            LogItem("ai_core");

            var soCount = Directory.GetFiles(ItemsFolder, "*.asset").Length;
            var prefabCount = Directory.GetFiles(PrefabsFolder, "*.prefab").Length;
            Debug.Log("SO 파일 " + soCount + "개 / 프리팹 " + prefabCount + "개");

            CheckPrefab("lobster", true);
            CheckPrefab("ai_core", false);
        }

        static void LogItem(string id)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemsFolder + "/" + id + ".asset");
            if (item == null)
            {
                Debug.LogError("SO 없음: " + id);
                return;
            }

            Debug.Log(
                id +
                " icon=" + (item.Icon != null ? item.Icon.name : "null") +
                " chip=" + item.Chip +
                " gold=" + item.Gold +
                " hasAnim=" + item.HasAnimation +
                " clip=" + (item.AnimationClip != null ? item.AnimationClip.name : "null") +
                " controller=" + (item.AnimatorController != null ? item.AnimatorController.name : "null") +
                " prefab=" + (item.Prefab != null ? item.Prefab.name : "null"));
        }

        static void CheckPrefab(string id, bool expectAnimator)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "/" + id + ".prefab");
            if (prefab == null)
            {
                Debug.LogError("프리팹 없음: " + id);
                return;
            }

            var animator = prefab.GetComponent<Animator>();
            var view = prefab.GetComponent<ItemView>();
            var renderer = prefab.GetComponent<SpriteRenderer>();
            var hasController = animator != null && animator.runtimeAnimatorController != null;
            if (expectAnimator && !hasController)
            {
                Debug.LogError("fish 프리팹에 Animator/controller 없음: " + id);
            }
            else if (!expectAnimator && animator != null)
            {
                Debug.LogError("scrap 프리팹에 Animator가 있음: " + id);
            }

            Debug.Log(
                "prefab " + id +
                " sprite=" + (renderer != null && renderer.sprite != null ? renderer.sprite.name : "null") +
                " animator=" + hasController +
                " itemView=" + (view != null && view.Data != null ? view.Data.Id : "null") +
                " collider=" + (prefab.GetComponent<Collider2D>() != null || prefab.GetComponent<Collider>() != null));
        }

        static string LoadJson()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (asset != null)
            {
                return asset.text;
            }

            if (File.Exists(JsonPath))
            {
                return File.ReadAllText(JsonPath);
            }

            return null;
        }

        static string PrepareJson(string json)
        {
            return json.Replace("\"animation\": null", "\"animation\":{}");
        }

        static ItemJsonRoot FallbackRoot(string json)
        {
            var alt = JsonUtility.FromJson<ItemJsonRootArray>(PrepareJson(json));
            if (alt == null || alt.items == null)
            {
                return null;
            }

            return new ItemJsonRoot
            {
                spriteSize = alt.spriteSize,
                chipToGoldRatio = alt.chipToGoldRatio,
                items = new List<ItemJson>(alt.items)
            };
        }

        static string ArtPath(string relative)
        {
            return (ArtRoot + relative).Replace('\\', '/');
        }

        static Sprite LoadIcon(string pngPath, string id)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
            var sprites = new List<Sprite>();
            if (assets != null)
            {
                for (var i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite sprite)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            if (sprites.Count == 0)
            {
                Debug.LogError("스프라이트가 없습니다: " + id + " / " + pngPath);
                return null;
            }

            sprites.Sort(CompareSprites);
            return sprites[0];
        }

        static int CompareSprites(Sprite a, Sprite b)
        {
            return CompareNatural(a.name, b.name);
        }

        static int CompareNatural(string a, string b)
        {
            var ia = 0;
            var ib = 0;
            while (ia < a.Length && ib < b.Length)
            {
                if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                {
                    var na = 0;
                    var nb = 0;
                    while (ia < a.Length && char.IsDigit(a[ia]))
                    {
                        na = na * 10 + (a[ia] - '0');
                        ia++;
                    }

                    while (ib < b.Length && char.IsDigit(b[ib]))
                    {
                        nb = nb * 10 + (b[ib] - '0');
                        ib++;
                    }

                    if (na != nb)
                    {
                        return na.CompareTo(nb);
                    }
                }
                else
                {
                    if (a[ia] != b[ib])
                    {
                        return a[ia].CompareTo(b[ib]);
                    }

                    ia++;
                    ib++;
                }
            }

            return (a.Length - ia).CompareTo(b.Length - ib);
        }

        static ItemCategory ParseCategory(string raw)
        {
            return string.Equals(raw, "scrap", StringComparison.OrdinalIgnoreCase)
                ? ItemCategory.Scrap
                : ItemCategory.Fish;
        }

        static ItemRarity ParseRarity(string raw)
        {
            switch ((raw ?? string.Empty).ToLowerInvariant())
            {
                case "uncommon":
                    return ItemRarity.Uncommon;
                case "rare":
                    return ItemRarity.Rare;
                case "epic":
                    return ItemRarity.Epic;
                case "legendary":
                    return ItemRarity.Legendary;
                default:
                    return ItemRarity.Common;
            }
        }

        static GameObject SaveItemPrefab(string id, ItemData data, string sortingLayer)
        {
            var prefabPath = PrefabsFolder + "/" + id + ".prefab";
            var exists = File.Exists(prefabPath);
            var root = exists
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(id);
            root.name = id;

            var renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = data.Icon;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = 5;

            var animator = root.GetComponent<Animator>();
            if (data.HasAnimation)
            {
                if (animator == null)
                {
                    animator = root.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = data.AnimatorController;
            }
            else if (animator != null)
            {
                UnityEngine.Object.DestroyImmediate(animator);
            }

            var view = root.GetComponent<ItemView>();
            if (view == null)
            {
                view = root.AddComponent<ItemView>();
            }

            view.Apply(data);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (exists)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        static string ResolveSortingLayer()
        {
            var layers = SortingLayer.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                var name = layers[i].name;
                if (name == "Objects" || name == "Items" || name == "Item")
                {
                    return name;
                }
            }

            return "Default";
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        [Serializable]
        class ItemJsonRootArray
        {
            public int spriteSize;
            public int chipToGoldRatio;
            public ItemJson[] items;
        }
    }
}
