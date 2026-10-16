using ScrapFishing.Audio;
using UnityEditor;
using UnityEngine;

public static class AudioPreviewMenu
{
    const string Root = "Scrap Fishing/Audio/";

    public static readonly string[] Keys =
    {
        "sfx_ui_tap",
        "sfx_start",
        "sfx_cast_whoosh",
        "sfx_splash",
        "sfx_reel",
        "sfx_zone_enter",
        "sfx_catch_common",
        "sfx_catch_rare",
        "sfx_catch_epic",
        "sfx_catch_legend",
        "sfx_catch_mutant",
        "sfx_chip_tick",
        "sfx_dive_ready",
        "sfx_dive_enter",
        "sfx_hazard",
        "sfx_mine_hit",
        "sfx_oxygen_warn",
        "sfx_surface",
        "sfx_timer_tick",
        "sfx_time_up",
        "sfx_new_best",
        "sfx_upgrade_buy",
        "sfx_upgrade_denied",
        "sfx_lure_full",
        "sfx_count_tick",
        "sfx_count_done",
        "sfx_zone_unlock",
        "sfx_upgrade_max",
        "sfx_legend_appear"
    };

    [MenuItem(Root + "Preview Keys", false, 1)]
    static void OpenPreview()
    {
        EditorWindow.GetWindow<AudioPreviewWindow>("SFX Preview");
    }

    [MenuItem(Root + "Play Title BGM", false, 40)]
    static void TitleBgm()
    {
        EnsurePlaymode();
        AudioManager.Ensure().Unlock();
        AudioManager.Ensure().PlayTitleBgm();
    }

    [MenuItem(Root + "Play Neo-Tokyo Sludge", false, 41)]
    static void PlayBgm()
    {
        EnsurePlaymode();
        AudioManager.Ensure().Unlock();
        AudioManager.Ensure().PlayPlayBgm();
    }

    [MenuItem(Root + "Reimport SFX + BGM", false, 60)]
    static void Reimport()
    {
        AssetDatabase.ImportAsset("Assets/Resources/Audio/SFX", ImportAssetOptions.ImportRecursive);
        AssetDatabase.ImportAsset("Assets/Resources/Audio/Start_Title_BGM.mp3", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Resources/Audio/Neo-Tokyo Sludge.mp3", ImportAssetOptions.ForceUpdate);
        Debug.Log("Audio reimport 요청 완료");
    }

    public static void Play(string key)
    {
        EnsurePlaymode();
        AudioManager.Ensure().Preview(key);
    }

    public static void EnsurePlaymode()
    {
        if (EditorApplication.isPlaying)
        {
            return;
        }

        Debug.Log("Play Mode에서 미리 들어보세요. 에디터 정지 상태면 AudioManager가 잠시 생성됩니다.");
        AudioManager.Ensure();
    }
}

public class AudioPreviewWindow : EditorWindow
{
    Vector2 _scroll;

    void OnGUI()
    {
        EditorGUILayout.LabelField("AudioManager.Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("키를 눌러 하나씩 들어보세요. Play Mode가 아니면 AudioManager가 잠시 생성됩니다.", MessageType.Info);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        var keys = AudioPreviewMenu.Keys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (GUILayout.Button(keys[i], GUILayout.Height(24)))
            {
                AudioPreviewMenu.Play(keys[i]);
            }
        }

        EditorGUILayout.EndScrollView();
    }
}

[CustomEditor(typeof(AudioManager))]
public class AudioManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var manager = (AudioManager)target;
        GUILayout.Space(8);
        GUILayout.Label("Preview", EditorStyles.boldLabel);
        var keys = AudioPreviewMenu.Keys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (GUILayout.Button("Play " + keys[i]))
            {
                manager.Preview(keys[i]);
            }
        }
    }
}
