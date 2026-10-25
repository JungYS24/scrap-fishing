using System;
using System.Collections.Generic;
using System.IO;
using ScrapFishing.Data;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    public static class ItemDataImporter
    {
        const string JsonPath = "Assets/Art/Scrap_items/fish_items.json";
        const string ArtRoot = "Assets/Art/Scrap_items/";
        const string ItemsFolder = "Assets/Data/Items";
        const string PrefabsFolder = "Assets/Prefabs/Items";
        const string DatabasePath = "Assets/Resources/ItemDatabase.asset";
        const int AnimationFrames = 8;

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

                var art = ArtSource(item, root.items);
                var pngPath = ArtPath(art.sprite);
                if (item.hasAnimation)
                {
                    CenterSheetPivots(pngPath);
                }

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
                    var anim = art.animation;
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
                data.SetFrames(item.hasAnimation ? LoadFrames(pngPath, item.id) : null);
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
            database.ReplaceZoneRarity(BuildZoneRarity(root.zoneRarity));
            EditorUtility.SetDirty(database);
            DeleteOrphans(root.items);

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

                if (item.HasAnimation && item.Frames.Length != AnimationFrames)
                {
                    errors++;
                    Debug.LogError("hasAnimation인데 프레임이 " + AnimationFrames + "개가 아닙니다: " + item.Id + " (" + item.Frames.Length + ")");
                }
                else if (!item.HasAnimation && item.Frames.Length > 0)
                {
                    Debug.LogWarning("정지 아이템인데 프레임이 있습니다: " + item.Id);
                }

                if (item.Chip <= 0 || item.Gold <= 0)
                {
                    errors++;
                    Debug.LogError("chip/gold 0 이하: " + item.Id + " chip=" + item.Chip + " gold=" + item.Gold);
                }

                if (item.Rarity >= ItemRarity.Epic && !item.HasAnimation)
                {
                    errors++;
                    Debug.LogError("epic 이상인데 hasAnimation=false: " + item.Id);
                }
                else if ((item.Rarity == ItemRarity.Normal || item.Rarity == ItemRarity.Rare) && item.HasAnimation)
                {
                    Debug.LogWarning("normal/rare인데 hasAnimation=true: " + item.Id);
                }
            }

            var rows = database.ZoneRarity;
            if (rows == null || rows.Count != 4)
            {
                errors++;
                Debug.LogError("zoneRarity 행이 4개가 아닙니다: " + (rows == null ? 0 : rows.Count));
            }
            else
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var sum = row.normal + row.rare + row.epic + row.legend + row.synchro;
                    if (sum <= 0)
                    {
                        errors++;
                        Debug.LogError("zoneRarity 합이 0: index=" + row.index + " name=" + row.name);
                    }
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
                zoneRarity = alt.zoneRarity,
                items = new List<ItemJson>(alt.items)
            };
        }

        // 자기 스프라이트가 없고 placeholder가 있으면 그 아이템의 스프라이트·애니메이션을 빌린다.
        static ItemJson ArtSource(ItemJson item, List<ItemJson> items)
        {
            if (string.IsNullOrEmpty(item.placeholder) || HasSprite(ArtPath(item.sprite)))
            {
                return item;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var other = items[i];
                if (other != null && other.id == item.placeholder)
                {
                    Debug.LogWarning("스프라이트 없음, 임시로 " + other.id + " 사용: " + item.id + " / " + ArtPath(item.sprite));
                    return other;
                }
            }

            Debug.LogError("placeholder 아이템이 없습니다: " + item.id + " -> " + item.placeholder);
            return item;
        }

        static bool HasSprite(string pngPath)
        {
            return !string.IsNullOrEmpty(pngPath) && AssetDatabase.LoadAssetAtPath<Sprite>(pngPath) != null;
        }

        static string ArtPath(string relative)
        {
            return (ArtRoot + relative).Replace('\\', '/');
        }

        // 에픽 이상 루프 애니메이션 프레임: 시트에서 잘린 스프라이트를 이름 끝 번호(_0 ~ _7) 순서로 정렬한다.
        // 프레임 이름이 파일명과 다른 시트(Sucker_Squid → Octopus_SS_n)가 있어서 이름 앞부분은 보지 않는다.
        static Sprite[] LoadFrames(string pngPath, string id)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pngPath))
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            sprites.Sort((a, b) =>
            {
                var byNumber = FrameNumber(a.name).CompareTo(FrameNumber(b.name));
                return byNumber != 0 ? byNumber : CompareNatural(a.name, b.name);
            });
            if (sprites.Count != AnimationFrames)
            {
                Debug.LogWarning("프레임이 " + AnimationFrames + "개가 아닙니다: " + id + " / " + pngPath + " (" + sprites.Count + "개)");
            }

            return sprites.ToArray();
        }

        static int FrameNumber(string name)
        {
            var end = name.Length;
            var start = end;
            while (start > 0 && char.IsDigit(name[start - 1]))
            {
                start--;
            }

            return start < end && int.TryParse(name.Substring(start, end - start), out var number) ? number : int.MaxValue;
        }

        // 잘린 프레임의 피벗을 모두 중앙으로 맞춘다(칼날비늘용 시트만 아래 가운데였다). 바뀐 것이 있을 때만 다시 가져온다.
        static void CenterSheetPivots(string pngPath)
        {
            if (!(AssetImporter.GetAtPath(pngPath) is TextureImporter importer) || importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                return;
            }

            // 예전 spritesheet 속성은 Unity 6에서 메타에 저장되지 않아 스프라이트 편집 데이터 제공자로 바꾼다.
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            var changed = false;
            foreach (var rect in rects)
            {
                if (rect.alignment != SpriteAlignment.Center || rect.pivot != new Vector2(0.5f, 0.5f))
                {
                    rect.alignment = SpriteAlignment.Center;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    changed = true;
                }
            }

            if (changed)
            {
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
                Debug.Log("시트 프레임 피벗을 중앙으로 맞췄습니다: " + pngPath);
            }
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

        static List<ZoneRarityWeights> BuildZoneRarity(List<ZoneRarityJson> rows)
        {
            var result = new List<ZoneRarityWeights>();
            if (rows == null || rows.Count != 4)
            {
                Debug.LogError("zoneRarity 행이 4개(0~3)가 아닙니다: " + (rows == null ? 0 : rows.Count));
                return result;
            }

            var seen = new bool[4];
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    Debug.LogError("zoneRarity[" + i + "] 가 null 입니다.");
                    continue;
                }

                var sum = row.normal + row.rare + row.epic + row.legend + row.synchro;
                if (sum <= 0 || row.index < 0 || row.index > 3)
                {
                    Debug.LogError("zoneRarity 행 오류: index=" + row.index + " sum=" + sum);
                }
                else
                {
                    seen[row.index] = true;
                }

                result.Add(new ZoneRarityWeights
                {
                    index = row.index,
                    name = row.name,
                    normal = row.normal,
                    rare = row.rare,
                    epic = row.epic,
                    legend = row.legend,
                    synchro = row.synchro
                });
            }

            for (var i = 0; i < seen.Length; i++)
            {
                if (!seen[i])
                {
                    Debug.LogError("zoneRarity에 index " + i + " 행이 없습니다.");
                }
            }

            return result;
        }

        static void DeleteOrphans(List<ItemJson> items)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (items != null)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && !string.IsNullOrEmpty(items[i].id))
                    {
                        ids.Add(items[i].id);
                    }
                }
            }

            DeleteOrphanFolder(ItemsFolder, "*.asset", ids, true);
            DeleteOrphanFolder(PrefabsFolder, "*.prefab", ids, false);
        }

        static void DeleteOrphanFolder(string folder, string pattern, HashSet<string> ids, bool itemDataOnly)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            var files = Directory.GetFiles(folder, pattern);
            for (var i = 0; i < files.Length; i++)
            {
                var fileName = Path.GetFileName(files[i]);
                var id = Path.GetFileNameWithoutExtension(fileName);
                if (ids.Contains(id))
                {
                    continue;
                }

                var assetPath = folder + "/" + fileName;
                if (itemDataOnly)
                {
                    if (AssetDatabase.LoadAssetAtPath<ItemData>(assetPath) == null)
                    {
                        continue;
                    }
                }
                else if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
                {
                    continue;
                }

                Debug.Log("고아 에셋 삭제: " + id + " / " + assetPath);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        static ItemCategory ParseCategory(string raw)
        {
            return string.Equals(raw, "scrap", StringComparison.OrdinalIgnoreCase)
                ? ItemCategory.Scrap
                : ItemCategory.Fish;
        }

        static ItemRarity ParseRarity(string raw)
        {
            switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "normal":
                    return ItemRarity.Normal;
                case "rare":
                    return ItemRarity.Rare;
                case "epic":
                    return ItemRarity.Epic;
                case "legend":
                    return ItemRarity.Legend;
                case "synchro":
                    return ItemRarity.Synchro;
                default:
                    Debug.LogWarning("알 수 없는 rarity '" + raw + "' → Normal");
                    return ItemRarity.Normal;
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
            public List<ZoneRarityJson> zoneRarity;
            public ItemJson[] items;
        }
    }
}
