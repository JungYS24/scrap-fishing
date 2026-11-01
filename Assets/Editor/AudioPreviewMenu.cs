using System;
using ScrapFishing.Audio;
using UnityEditor;
using UnityEngine;

public static class AudioPreviewMenu
{
    const string Root = "Scrap Fishing/Audio/";

    public static readonly SfxId[] Ids = (SfxId[])Enum.GetValues(typeof(SfxId));

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

    [MenuItem(Root + "Reimport SFX", false, 60)]
    static void Reimport()
    {
        AssetDatabase.ImportAsset("Assets/Resources/Audio/SFX", ImportAssetOptions.ImportRecursive);
        // BGM 두 곡은 StreamingAssets에서 실행 중에 받으므로 다시 가져올 것이 없다.
        Debug.Log("Audio reimport 요청 완료");
    }

    public static void Play(SfxId id)
    {
        EnsurePlaymode();
        AudioManager.Ensure().Preview(id);
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
        EditorGUILayout.LabelField("SoundCue Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("큐를 눌러 하나씩 들어보세요. Play Mode가 아니면 AudioManager가 잠시 생성됩니다.", MessageType.Info);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        var ids = AudioPreviewMenu.Ids;
        for (var i = 0; i < ids.Length; i++)
        {
            if (GUILayout.Button(ids[i].ToString(), GUILayout.Height(24)))
            {
                AudioPreviewMenu.Play(ids[i]);
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
        var ids = AudioPreviewMenu.Ids;
        for (var i = 0; i < ids.Length; i++)
        {
            if (GUILayout.Button("Play " + ids[i]))
            {
                manager.Preview(ids[i]);
            }
        }
    }
}
