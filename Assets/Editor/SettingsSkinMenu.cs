using System.IO;
using ScrapFishing.UI;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    // Assets/scrap_fishing_ui/settings의 스프라이트를 묶어 Resources/UI/SettingsSkin.asset을 만들거나 다시 채운다.
    public static class SettingsSkinMenu
    {
        public const string AssetPath = "Assets/Resources/UI/SettingsSkin.asset";

        [MenuItem("Tools/Scrap Fishing/Create Settings Skin")]
        public static void Create()
        {
            var skin = AssetDatabase.LoadAssetAtPath<SettingsSkin>(AssetPath);
            var created = skin == null;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                skin = ScriptableObject.CreateInstance<SettingsSkin>();
            }

            skin.panel = Load("panel");
            skin.headerBar = Load("header_bar");
            skin.close = Load("btn_close");
            skin.soundOn = Load("btn_sound_on");
            skin.soundOff = Load("btn_sound_off");
            skin.labelBox = Load("label_box");
            skin.sliderTrack = Load("slider_track");
            skin.sliderFill = Load("slider_fill");
            skin.sliderKnob = Load("slider_knob");
            skin.wideCyan = Load("btn_wide_cyan");
            skin.wideGray = Load("btn_wide_gray");
            skin.wideYellow = Load("btn_wide_yellow");
            skin.wideGreen = Load("btn_wide_green");
            skin.footerBox = Load("footer_box");

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
            Debug.Log("설정 패널 스킨을 " + (created ? "만들었습니다" : "다시 채웠습니다") + ": " + AssetPath + (skin.Complete ? "" : " (빠진 스프라이트 있음)"));
        }

        static Sprite Load(string name)
        {
            var path = SettingsUiImportPostprocessor.Root + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("설정 패널 스킨 스프라이트를 찾지 못했습니다: " + path);
            }

            return sprite;
        }
    }
}
