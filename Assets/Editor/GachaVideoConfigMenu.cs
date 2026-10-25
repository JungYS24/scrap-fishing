using System.IO;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.EditorTools
{
    // Resources/Gacha/GachaVideoConfig.asset을 기본값(머신별 영상·효과음·섬광·결과 카드 시점)으로 만든다.
    // 이미 있으면 덮어쓰지 않고 선택만 한다(Inspector에서 고친 값을 지킨다).
    public static class GachaVideoConfigMenu
    {
        public const string AssetPath = "Assets/Resources/Gacha/GachaVideoConfig.asset";

        [MenuItem("Tools/Scrap Fishing/Create Gacha Video Config")]
        public static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GachaVideoConfig>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                Debug.Log("오리파 연출 설정이 이미 있습니다: " + AssetPath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var config = GachaVideoConfig.CreateDefault();
            foreach (var entry in config.machines)
            {
                if (entry.sfx == null)
                {
                    Debug.LogWarning("오리파 연출 효과음을 찾지 못했습니다: Resources/" + GachaVideoConfig.SfxPath(entry.machineId));
                }

                if (!File.Exists(Path.Combine(Application.streamingAssetsPath, GachaVideoConfig.VideoFolder, entry.videoFile)))
                {
                    Debug.LogWarning("오리파 연출 영상을 찾지 못했습니다: StreamingAssets/" + GachaVideoConfig.VideoFolder + "/" + entry.videoFile);
                }
            }

            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log("오리파 연출 설정을 만들었습니다: " + AssetPath);
        }
    }
}
