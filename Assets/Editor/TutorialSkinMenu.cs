using System.IO;
using ScrapFishing.UI;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    // Assets/scrap_fishing_ui/tutorial의 스프라이트를 묶어 Resources/UI/TutorialSkin.asset을 만들거나 다시 채운다.
    // 튜토리얼 PNG를 가져올 때 TutorialUiImportPostprocessor가 자동으로 부른다.
    public static class TutorialSkinMenu
    {
        public const string AssetPath = "Assets/Resources/UI/TutorialSkin.asset";

        [MenuItem("Tools/Scrap Fishing/Create Tutorial Skin")]
        public static void Create()
        {
            Fill(true);
        }

        public static void Fill(bool select)
        {
            var skin = AssetDatabase.LoadAssetAtPath<TutorialSkin>(AssetPath);
            var created = skin == null;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                skin = ScriptableObject.CreateInstance<TutorialSkin>();
            }

            skin.panel = Load("tut_panel");
            skin.tab = Load("tut_tab");
            skin.button = Load("tut_button");
            skin.dotOn = Load("tut_dot_on");
            skin.dotOff = Load("tut_dot_off");
            skin.bracket = Load("tut_bracket_tl");
            skin.glow = Load("tut_glow");
            skin.arrowRight = Load("tut_arrow_right");
            skin.arrowDown = Load("tut_arrow_down");
            skin.hand = Load("tut_hand");
            skin.ring = Load("tut_ring");

            if (created)
            {
                AssetDatabase.CreateAsset(skin, AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(skin);
            }

            AssetDatabase.SaveAssets();
            if (select)
            {
                Selection.activeObject = skin;
            }

            Debug.Log("튜토리얼 스킨을 " + (created ? "만들었습니다" : "다시 채웠습니다") + ": " + AssetPath + (skin.Complete ? "" : " (빠진 스프라이트 있음)"));
        }

        static Sprite Load(string name)
        {
            var path = TutorialUiImportPostprocessor.Root + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("튜토리얼 스킨 스프라이트를 찾지 못했습니다: " + path);
            }

            return sprite;
        }
    }
}
