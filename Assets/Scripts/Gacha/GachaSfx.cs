using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ScrapFishing.Gacha
{
    // 오리파 연출 효과음(머신마다 영상과 같은 길이 10초). 첫 다운로드에서 빼려고 StreamingAssets/Audio/Gacha/gacha_<머신>_sfx.mp3에 두고,
    // 오리파 화면을 열 때 6개를 미리 받아 둔다(첫 뽑기에서 늦게 나오지 않게). ogg는 iOS 사파리가 못 틀어서 mp3로 바꿨다(원본은 Art/Audio/GachaSfxSource).
    // 받은 소리는 풀어서 계속 둔다(6개. 10초 스테레오라 장당 약 3.8MB, 예전 Resources ogg도 웹에서는 똑같이 풀려 있었다).
    public static class GachaSfx
    {
        public const string Folder = "Audio/Gacha/";
        public const int MaxAttempts = 3;

        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> Loading = new HashSet<string>();
        static readonly Dictionary<string, int> Failures = new Dictionary<string, int>();

        public static string FileOf(string machineId)
        {
            return Folder + "gacha_" + GachaVideoConfig.KeyOf(machineId) + "_sfx.mp3";
        }

        public static string Url(string machineId)
        {
            var url = Application.streamingAssetsPath + "/" + FileOf(machineId);
            return url.Contains("://") ? url : "file://" + url;
        }

        public static void Preload(IEnumerable<GachaMachineData> machines)
        {
            if (machines == null)
            {
                return;
            }

            foreach (var machine in machines)
            {
                if (machine != null)
                {
                    Load(machine.Id);
                }
            }
        }

        // 받은 효과음. 아직 받는 중이거나 받지 못했으면 null을 주고, 없으면 받기를 시작한다.
        public static AudioClip Get(string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return null;
            }

            // "machine_lure"(상점)과 "lure"(연출 설정)를 같은 소리로 본다.
            machineId = GachaVideoConfig.KeyOf(machineId);
            if (Clips.TryGetValue(machineId, out var clip) && clip != null)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }

                return clip;
            }

            Load(machineId);
            return null;
        }

        // 받았고 소리 데이터까지 풀려서 바로 틀 수 있는지.
        public static bool IsLoaded(string machineId)
        {
            return machineId != null && Clips.TryGetValue(GachaVideoConfig.KeyOf(machineId), out var clip) && clip != null && clip.loadState == AudioDataLoadState.Loaded;
        }

        static void Load(string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return;
            }

            machineId = GachaVideoConfig.KeyOf(machineId);
            if (Clips.ContainsKey(machineId) || Loading.Contains(machineId)
                || (Failures.TryGetValue(machineId, out var failed) && failed >= MaxAttempts))
            {
                return;
            }

            Loading.Add(machineId);
            var request = UnityWebRequestMultimedia.GetAudioClip(Url(machineId), AudioType.MPEG);
            var handler = (DownloadHandlerAudioClip)request.downloadHandler;
            // 처음부터 끝까지 받아 풀어 둔다(뽑기 때 바로, 영상 시간에 맞춰 중간부터도 틀 수 있게).
            handler.streamAudio = false;
            handler.compressed = false;
            request.SendWebRequest().completed += _ =>
            {
                Loading.Remove(machineId);
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var clip = DownloadHandlerAudioClip.GetContent(request);
                    clip.name = "gacha_" + GachaVideoConfig.KeyOf(machineId) + "_sfx";
                    clip.LoadAudioData();
                    Clips[machineId] = clip;
                }
                else
                {
                    Failures[machineId] = (Failures.TryGetValue(machineId, out var count) ? count : 0) + 1;
                    Debug.LogWarning("오리파 효과음을 받지 못했습니다: " + request.url + " / " + request.error);
                }

                request.Dispose();
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clips.Clear();
            Loading.Clear();
            Failures.Clear();
        }
    }
}
