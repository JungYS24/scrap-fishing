using System.IO;
using System.Linq;
using ScrapFishing.Dive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScrapFishing.Build
{
    public static class DiveSceneryBuilder
    {
        const string ScenePath = "Assets/Scenes/Boat.unity";
        const string ArtPath = "Assets/Resources/DiveArt.asset";
        const string PngRoot = "Assets/Art/External/Dive/underwater-diving-files/PNG/";
        const string FadedMidgroundPath = "Assets/Art/Generated/MidgroundFaded.png";
        const int FadeRows = 90;
        static readonly string[] Replaced = { "Background", "Midground", "Fish" };

        [MenuItem("Scrap Fishing/Build Dive Scenery")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ConfigureTextures();
            BuildArtAsset();
            var rocks = BuildFadedMidground();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var world = GameObject.Find("World");
            if (world == null)
            {
                Debug.LogError("Boat 씬에 World가 없습니다.");
                return;
            }

            if (!Application.isBatchMode && world.transform.Find("Midground")?.GetComponent<DiveParallax>() != null &&
                !EditorUtility.DisplayDialog("Build Dive Scenery", "이미 만들어진 다이브 배경이 있습니다. 씬에서 옮긴 배치는 사라집니다. 다시 만들까요?", "다시 만들기", "취소"))
            {
                return;
            }

            foreach (var name in Replaced)
            {
                var old = world.transform.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            BuildBackground(world.transform).SetActive(false);
            BuildMidground(world.transform, rocks).SetActive(false);
            var player = world.transform.Find("Player");
            if (player != null)
            {
                player.localScale = Vector3.one * 1.25f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Dive scenery built: " + ScenePath);
        }

        static void ConfigureTextures()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PngRoot.TrimEnd('/') });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }

                if (importer.filterMode == FilterMode.Point &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    !importer.mipmapEnabled)
                {
                    continue;
                }

                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        static void BuildArtAsset()
        {
            var art = AssetDatabase.LoadAssetAtPath<DiveArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<DiveArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            art.FishSmall = LoadTexture("enemies/fish.png");
            art.FishDart = LoadTexture("enemies/fish-dart.png");
            art.FishBig = LoadTexture("enemies/fish-big.png");
            art.PlayerIdle = LoadTexture("player/player-idle.png");
            art.PlayerSwim = LoadTexture("player/player-swiming.png");
            art.PlayerHurt = LoadTexture("player/player-hurt.png");
            art.Bubbles = LoadTexture("FX/bubbles.png");
            art.ExplosionSmall = LoadTexture("FX/explosion-small.png");
            art.ExplosionMedium = LoadTexture("FX/explosion.png");
            art.ExplosionBig = LoadTexture("FX/explosion-big.png");
            art.MineSmall = LoadSprite("enemies/mine-small.png", "mine-small_0");
            art.MineMedium = LoadSprite("enemies/mine.png", "mine_0");
            art.MineBig = LoadSprite("enemies/mine-big.png", "mine-big_0");
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        static GameObject BuildBackground(Transform world)
        {
            var root = Group(world, "Background", 0.04f);
            var tile = LoadSprite("environment/background.png", "background_0");
            Place(root, "WaterTop", tile, new Vector2(0f, 2.56f), 2f, -20, Color.white);
            Place(root, "WaterBottom", tile, new Vector2(0f, -2.56f), 2f, -20, Color.white, true);

            var far = Group(root, "FarLayer", 0.07f);
            var distant = new Color(0.45f, 0.56f, 0.7f);
            Place(far, "FloatingRock", LoadSprite("environment/tiles.png", "tiles_19"), new Vector2(1.55f, 1.4f), 0.6f, -17, distant);
            Place(far, "Ruins", LoadSprite("environment/props.png", "props_4"), new Vector2(-1.15f, -2.85f), 0.9f, -16, distant);
            Place(far, "Statue", LoadSprite("environment/props.png", "props_3"), new Vector2(0f, -3f), 0.5f, -16, distant);
            return root.gameObject;
        }

        static GameObject BuildMidground(Transform world, Sprite rocks)
        {
            var root = Group(world, "Midground", 0.1f);
            Place(root, "Rocks", rocks, new Vector2(0f, -2.95f), 0.55f, -10, Color.white);
            Place(root, "RockLeft", LoadSprite("environment/props.png", "props_5"), new Vector2(-2f, -3.8f), 0.55f, -9, Color.white, true);
            Place(root, "RockRight", LoadSprite("environment/props.png", "props_0"), new Vector2(1.9f, -3.75f), 0.6f, -9, Color.white);

            var floor = LoadSprite("environment/tiles.png", "tiles_6");
            for (var i = -1; i <= 1; i++)
            {
                Place(root, "Floor" + (i + 1), floor, new Vector2(i * 1.78f, -4.05f), 1f, -8, Color.white, i == 0);
            }

            var bush = LoadSprite("environment/props.png", "props_7");
            var coral = LoadSprite("environment/props.png", "props_8");
            var weed = LoadSprite("environment/props.png", "props_9");
            Place(root, "Coral0", coral, new Vector2(-2.15f, -3.6f), 1f, -7, Color.white);
            Place(root, "Bush0", bush, new Vector2(-1.5f, -3.55f), 1f, -7, Color.white);
            Place(root, "Coral1", coral, new Vector2(-0.55f, -3.55f), 1f, -7, Color.white, true);
            Place(root, "Seaweed0", weed, new Vector2(0.35f, -3.5f), 1f, -7, Color.white);
            Place(root, "Bush1", bush, new Vector2(1.25f, -3.6f), 1f, -7, Color.white, true);
            Place(root, "Seaweed1", weed, new Vector2(2.05f, -3.5f), 1f, -7, Color.white, true);
            return root.gameObject;
        }

        static Sprite BuildFadedMidground()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FadedMidgroundPath));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(PngRoot + "environment/midground.png"));
            var pixels = texture.GetPixels32();
            var width = texture.width;
            var height = texture.height;
            for (var row = 0; row < FadeRows; row++)
            {
                var keep = Mathf.SmoothStep(0f, 1f, row / (float)FadeRows);
                var y = height - 1 - row;
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    pixels[i].a = (byte)(pixels[i].a * keep);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(FadedMidgroundPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FadedMidgroundPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(FadedMidgroundPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(FadedMidgroundPath);
        }

        static Transform Group(Transform parent, string name, float parallax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<DiveParallax>();
            var so = new SerializedObject(layer);
            so.FindProperty("factor").floatValue = parallax;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }

        static void Place(Transform parent, string name, Sprite sprite, Vector2 position, float scale, int order, Color color, bool flip = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = color;
            renderer.flipX = flip;
        }

        static Texture2D LoadTexture(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PngRoot + file);
        }

        static Sprite LoadSprite(string file, string name)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(PngRoot + file).OfType<Sprite>().FirstOrDefault(s => s.name == name);
            if (sprite == null)
            {
                Debug.LogWarning($"Sprite not found: {file} / {name}");
            }

            return sprite;
        }
    }
}
