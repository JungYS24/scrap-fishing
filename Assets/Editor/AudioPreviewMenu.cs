using ScrapFishing.Audio;
using UnityEditor;
using UnityEngine;

public static class AudioPreviewMenu
{
    const string Root = "Scrap Fishing/Audio/";

    [MenuItem(Root + "Play sfx_ui_tap", false, 10)]
    static void UiTap() => Play("sfx_ui_tap");

    [MenuItem(Root + "Play sfx_start", false, 11)]
    static void StartClip() => Play("sfx_start");

    [MenuItem(Root + "Play sfx_cast_whoosh", false, 12)]
    static void Cast() => Play("sfx_cast_whoosh");

    [MenuItem(Root + "Play sfx_splash", false, 13)]
    static void Splash() => Play("sfx_splash");

    [MenuItem(Root + "Play sfx_reel", false, 14)]
    static void Reel() => Play("sfx_reel");

    [MenuItem(Root + "Play sfx_zone_enter", false, 15)]
    static void Zone() => Play("sfx_zone_enter");

    [MenuItem(Root + "Play sfx_catch_common", false, 16)]
    static void CatchCommon() => Play("sfx_catch_common");

    [MenuItem(Root + "Play sfx_catch_rare", false, 17)]
    static void CatchRare() => Play("sfx_catch_rare");

    [MenuItem(Root + "Play sfx_chip_tick", false, 18)]
    static void Chip() => Play("sfx_chip_tick");

    [MenuItem(Root + "Play sfx_dive_ready", false, 19)]
    static void DiveReady() => Play("sfx_dive_ready");

    [MenuItem(Root + "Play sfx_timer_tick", false, 20)]
    static void Timer() => Play("sfx_timer_tick");

    [MenuItem(Root + "Play sfx_time_up", false, 21)]
    static void TimeUp() => Play("sfx_time_up");

    [MenuItem(Root + "Play sfx_upgrade_buy", false, 22)]
    static void Buy() => Play("sfx_upgrade_buy");

    [MenuItem(Root + "Play sfx_upgrade_denied", false, 23)]
    static void Denied() => Play("sfx_upgrade_denied");

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

    static void Play(string key)
    {
        EnsurePlaymode();
        AudioManager.Ensure().Preview(key);
    }

    static void EnsurePlaymode()
    {
        if (EditorApplication.isPlaying)
        {
            return;
        }

        Debug.Log("Play Mode에서 미리 들어보세요. 에디터 정지 상태면 AudioManager가 잠시 생성됩니다.");
        AudioManager.Ensure();
    }
}

[CustomEditor(typeof(AudioManager))]
public class AudioManagerEditor : Editor
{
    static readonly string[] Keys =
    {
        "sfx_ui_tap", "sfx_start", "sfx_cast_whoosh", "sfx_splash", "sfx_reel",
        "sfx_zone_enter", "sfx_catch_common", "sfx_catch_rare", "sfx_chip_tick",
        "sfx_dive_ready", "sfx_timer_tick", "sfx_time_up", "sfx_upgrade_buy", "sfx_upgrade_denied"
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var manager = (AudioManager)target;
        GUILayout.Space(8);
        GUILayout.Label("Preview", EditorStyles.boldLabel);
        for (var i = 0; i < Keys.Length; i++)
        {
            if (GUILayout.Button("Play " + Keys[i]))
            {
                manager.Preview(Keys[i]);
            }
        }
    }
}
