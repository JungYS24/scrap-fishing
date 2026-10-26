using System;
using System.IO;
using ScrapFishing.UI;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    // Assets/scrap_fishing_ui/hud의 스프라이트와 hud_layout.json 글자 영역을 묶어 Resources/UI/HudSkin.asset을 만들거나 다시 채운다.
    // 이미 있으면 hudScale(Inspector에서 고친 값)은 그대로 둔다.
    public static class HudSkinMenu
    {
        public const string AssetPath = "Assets/Resources/UI/HudSkin.asset";
        const string LayoutPath = HudUiImportPostprocessor.Root + "hud_layout.json";

        [Serializable]
        class Area
        {
            public float xMin;
            public float yMin;
            public float xMax;
            public float yMax;

            public Rect ToRect()
            {
                return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
        }

        [Serializable]
        class Panel
        {
            public Area numberArea;
            public Area barArea;
            public Area textArea;
        }

        [Serializable]
        class Layout
        {
            public Panel depth_panel;
            public Panel time_panel;
            public Panel scrap_panel;
            public Panel zone_tag;
        }

        [MenuItem("Tools/Scrap Fishing/Create HUD Skin")]
        public static void Create()
        {
            var skin = AssetDatabase.LoadAssetAtPath<HudSkin>(AssetPath);
            var created = skin == null;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                skin = ScriptableObject.CreateInstance<HudSkin>();
            }

            skin.depthPanel = Load("depth_panel");
            skin.zoneTag = Load("zone_tag");
            skin.scrapPanel = Load("scrap_panel");
            skin.scrapPanelFull = Load("scrap_panel_full");
            skin.scrapBarFill = Load("scrap_bar_fill");
            skin.timePanel = Load("time_panel");
            skin.timePanelWarning = Load("time_panel_warning");
            skin.diveFull = Load("dive_full");
            skin.diveEmpty = Load("dive_empty");

            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (json != null)
            {
                var layout = JsonUtility.FromJson<Layout>(json.text);
                skin.depthNumber = layout.depth_panel.numberArea.ToRect();
                skin.timeNumber = layout.time_panel.numberArea.ToRect();
                skin.scrapNumber = layout.scrap_panel.numberArea.ToRect();
                skin.scrapBar = layout.scrap_panel.barArea.ToRect();
                skin.zoneText = layout.zone_tag.textArea.ToRect();
            }
            else
            {
                Debug.LogWarning("HUD 글자 영역 파일을 찾지 못해 기본값을 씁니다: " + LayoutPath);
            }

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
            Debug.Log("HUD 스킨을 " + (created ? "만들었습니다" : "다시 채웠습니다") + ": " + AssetPath + (skin.Complete ? "" : " (빠진 스프라이트 있음)"));
        }

        static Sprite Load(string name)
        {
            var path = HudUiImportPostprocessor.Root + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("HUD 스킨 스프라이트를 찾지 못했습니다: " + path);
            }

            return sprite;
        }
    }
}
