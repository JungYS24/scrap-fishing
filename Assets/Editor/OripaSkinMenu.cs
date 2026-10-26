using System.IO;
using System.Linq;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    // Assets/scrap_fishing_ui/oripa의 스프라이트를 모두 묶어 Resources/UI/OripaSkin.asset을 만들거나 다시 채운다.
    public static class OripaSkinMenu
    {
        public const string AssetPath = "Assets/Resources/UI/OripaSkin.asset";

        [MenuItem("Tools/Scrap Fishing/Create Oripa Skin")]
        public static void Create()
        {
            var skin = AssetDatabase.LoadAssetAtPath<OripaSkin>(AssetPath);
            var created = skin == null;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                skin = ScriptableObject.CreateInstance<OripaSkin>();
            }

            skin.sprites = AssetDatabase.FindAssets("t:Sprite", new[] { OripaUiImportPostprocessor.Root.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToList();

            if (created)
            {
                AssetDatabase.CreateAsset(skin, AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(skin);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = skin;
            Debug.Log("오리파 스킨을 " + (created ? "만들었습니다" : "다시 채웠습니다") + ": " + AssetPath + " (" + skin.sprites.Count + "장)");
        }
    }
}
