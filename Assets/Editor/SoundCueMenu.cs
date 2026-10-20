using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ScrapFishing.Audio;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.AudioTools
{
    public static class SoundCueMenu
    {
        const string SfxFolder = "Assets/Resources/Audio/SFX";
        const string CueFolder = "Assets/Resources/Audio/Cues";
        const string LibraryPath = "Assets/Resources/Audio/SoundCueLibrary.asset";
        const float Jitter = 0.04f;

        readonly struct Seed
        {
            public readonly SfxId Id;
            public readonly float Volume;
            public readonly bool Jitter;
            public readonly float Cooldown;
            public readonly SfxId? Fallback;
            public readonly float FallbackPitch;
            public readonly SoundBus Bus;

            public Seed(SfxId id, float volume, bool jitter, float cooldown, SfxId? fallback, float fallbackPitch, SoundBus bus)
            {
                Id = id;
                Volume = volume;
                Jitter = jitter;
                Cooldown = cooldown;
                Fallback = fallback;
                FallbackPitch = fallbackPitch;
                Bus = bus;
            }
        }

        static readonly Seed[] Seeds =
        {
            new Seed(SfxId.UiTap, 0.6f, false, 0f, null, 1f, SoundBus.Ui),
            new Seed(SfxId.Start, 0.5f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.CastWhoosh, 0.55f, true, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.Splash, 0.65f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.Reel, 1f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.ZoneEnter, 0.5f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.CatchCommon, 0.5f, true, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.CatchRare, 0.8f, true, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.CatchMutant, 0.85f, true, 0f, SfxId.CatchRare, 1f, SoundBus.Game),
            new Seed(SfxId.CatchEpic, 0.85f, true, 0f, SfxId.CatchRare, 0.9f, SoundBus.Game),
            new Seed(SfxId.CatchLegend, 0.9f, true, 0f, SfxId.CatchRare, 0.8f, SoundBus.Game),
            new Seed(SfxId.ChipTick, 0.35f, true, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.DiveReady, 0.45f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.DiveEnter, 0.4f, false, 0f, SfxId.DiveReady, 1f, SoundBus.Game),
            new Seed(SfxId.Surface, 0.4f, false, 0f, SfxId.Splash, 1f, SoundBus.Game),
            new Seed(SfxId.Hazard, 0.5f, false, 0f, SfxId.UpgradeDenied, 1f, SoundBus.Game),
            new Seed(SfxId.MineHit, 0.5f, false, 0f, SfxId.UpgradeDenied, 1f, SoundBus.Game),
            new Seed(SfxId.OxygenWarn, 0.45f, false, 0f, SfxId.TimerTick, 1f, SoundBus.Game),
            new Seed(SfxId.TimerTick, 0.35f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.TimeUp, 0.5f, false, 0f, null, 1f, SoundBus.Game),
            new Seed(SfxId.NewBest, 0.5f, false, 0f, SfxId.DiveReady, 1f, SoundBus.Game),
            new Seed(SfxId.UpgradeBuy, 0.65f, false, 0f, null, 1f, SoundBus.Ui),
            new Seed(SfxId.UpgradeDenied, 0.8f, false, 0f, null, 1f, SoundBus.Ui),
            new Seed(SfxId.UpgradeMax, 0.6f, false, 0f, SfxId.NewBest, 1.15f, SoundBus.Ui),
            new Seed(SfxId.ZoneUnlock, 0.55f, false, 0f, SfxId.ZoneEnter, 1.2f, SoundBus.Game),
            new Seed(SfxId.LureFull, 0.5f, false, 0.5f, SfxId.UpgradeDenied, 1f, SoundBus.Game),
            new Seed(SfxId.CountTick, 0.35f, false, 0f, SfxId.ChipTick, 1f, SoundBus.Ui),
            new Seed(SfxId.CountDone, 0.55f, false, 0f, SfxId.UpgradeBuy, 1f, SoundBus.Ui),
            new Seed(SfxId.LegendAppear, 0.6f, false, 0f, SfxId.CatchRare, 1.35f, SoundBus.Game)
        };

        [MenuItem("ScrapFishing/Audio/Build Sound Cues")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(CueFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Audio", "Cues");
            }

            var clipsByToken = IndexClips();
            var cues = new Dictionary<SfxId, SoundCue>();
            var created = new HashSet<SfxId>();
            var updated = 0;
            foreach (var seed in Seeds)
            {
                var path = CueFolder + "/Cue_" + seed.Id + ".asset";
                var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(path);
                var clips = ClipsFor(seed.Id, clipsByToken);
                if (cue == null)
                {
                    cue = ScriptableObject.CreateInstance<SoundCue>();
                    cue.id = seed.Id;
                    cue.volume = seed.Volume;
                    cue.pitchMin = seed.Jitter ? 1f - Jitter : 1f;
                    cue.pitchMax = seed.Jitter ? 1f + Jitter : 1f;
                    cue.cooldown = seed.Cooldown;
                    cue.maxPerFrame = 2;
                    cue.bus = seed.Bus;
                    cue.fallbackPitch = seed.FallbackPitch;
                    cue.clips = clips;
                    AssetDatabase.CreateAsset(cue, path);
                    created.Add(seed.Id);
                }
                else
                {
                    cue.id = seed.Id;
                    cue.clips = clips;
                    EditorUtility.SetDirty(cue);
                    updated++;
                }

                cues[seed.Id] = cue;
            }

            foreach (var seed in Seeds)
            {
                if (!created.Contains(seed.Id) || seed.Fallback == null)
                {
                    continue;
                }

                cues[seed.Id].fallback = cues[seed.Fallback.Value];
                EditorUtility.SetDirty(cues[seed.Id]);
            }

            var library = AssetDatabase.LoadAssetAtPath<SoundCueLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundCueLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var ordered = new SoundCue[Seeds.Length];
            for (var i = 0; i < Seeds.Length; i++)
            {
                ordered[i] = cues[Seeds[i].Id];
            }

            library.cues = ordered;
            library.Rebuild();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SoundCue 생성 " + created.Count + "개, 클립만 갱신 " + updated + "개. 라이브러리 " + LibraryPath);
        }

        [MenuItem("ScrapFishing/Audio/Validate Sound Cues")]
        public static void Validate()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundCueLibrary>(LibraryPath);
            var report = new StringBuilder();
            report.AppendLine("SoundCue 검증");
            report.AppendLine("Id | clips | sec | peak | quiet | 재생");
            var missing = new List<string>();
            var cycles = new List<string>();
            if (library == null || library.cues == null)
            {
                report.AppendLine("라이브러리가 없습니다. Build Sound Cues를 먼저 실행하세요.");
                Debug.LogWarning(report.ToString());
                return;
            }

            var seen = new HashSet<SfxId>();
            foreach (var cue in library.cues)
            {
                if (cue == null)
                {
                    report.AppendLine("(null cue)");
                    continue;
                }

                if (!seen.Add(cue.id))
                {
                    report.AppendLine(cue.id + " | 중복 id");
                }

                var playable = 0;
                var quiet = 0;
                var minLen = float.MaxValue;
                var maxLen = 0f;
                var peak = 0f;
                var measured = false;
                if (cue.clips != null)
                {
                    foreach (var clip in cue.clips)
                    {
                        if (clip == null)
                        {
                            continue;
                        }

                        minLen = Mathf.Min(minLen, clip.length);
                        maxLen = Mathf.Max(maxLen, clip.length);
                        if (SfxLoudness.TryMeasurePeak(clip, out var clipPeak))
                        {
                            measured = true;
                            peak = Mathf.Max(peak, clipPeak);
                            if (clipPeak < SfxLoudness.QuietPeak)
                            {
                                quiet++;
                            }
                            else
                            {
                                playable++;
                            }
                        }
                        else if (!SfxLoudness.IsTooQuiet(clip))
                        {
                            playable++;
                        }
                        else
                        {
                            quiet++;
                        }
                    }
                }

                var clipCount = cue.clips != null ? cue.clips.Length : 0;
                var length = clipCount == 0 ? "-" : minLen == maxLen ? minLen.ToString("0.00") : minLen.ToString("0.00") + "~" + maxLen.ToString("0.00");
                var peakText = measured ? SfxLoudness.PeakDb(peak).ToString("0.0") + " dB" : "-";
                var how = playable > 0 ? "자체" : FallbackHow(cue);
                report.AppendLine(cue.id + " | " + clipCount + " | " + length + " | " + peakText + " | " + (quiet > 0 ? "yes" : "no") + " | " + how);
                if (playable == 0)
                {
                    missing.Add(cue.id + " → " + how);
                }

                if (HasCycle(cue))
                {
                    cycles.Add(cue.id.ToString());
                }
            }

            report.AppendLine(cycles.Count == 0 ? "순환 참조: 없음" : "순환 참조: " + string.Join(", ", cycles));
            report.AppendLine(QuietFileLine());
            report.AppendLine("파일 없는 큐 (추가 제작):");
            if (missing.Count == 0)
            {
                report.AppendLine("- 없음");
            }
            else
            {
                foreach (var line in missing)
                {
                    report.AppendLine("- " + line);
                }
            }

            report.AppendLine("게임에서만 다른 경로: CountTick은 자체 → ChipTick → UiTap(피치 1.3~1.4), LureFull은 자체 클립이 없으면 UpgradeDenied를 볼륨 0.30으로, UpgradeMax는 NewBest에 클립이 없으면 합성음을 피치 1.15로 냅니다.");
            report.AppendLine(ImportStatus());
            Debug.Log(report.ToString());
        }

        public static void BuildThenValidate()
        {
            Build();
            Validate();
        }

        static string FallbackHow(SoundCue cue)
        {
            if (cue.fallback != null && cue.fallback.clips != null)
            {
                foreach (var clip in cue.fallback.clips)
                {
                    if (clip != null && !SfxLoudness.IsTooQuiet(clip))
                    {
                        return "폴백 " + cue.fallback.id;
                    }
                }
            }

            return "합성음";
        }

        static bool HasCycle(SoundCue start)
        {
            var seen = new HashSet<SoundCue>();
            var cue = start;
            while (cue != null)
            {
                if (!seen.Add(cue))
                {
                    return true;
                }

                cue = cue.fallback;
            }

            return false;
        }

        static string QuietFileLine()
        {
            var notes = new List<string>();
            foreach (var pair in IndexClips())
            {
                foreach (var clip in pair.Value)
                {
                    if (clip == null || !SfxLoudness.TryMeasurePeak(clip, out var peak) || peak >= SfxLoudness.QuietPeak)
                    {
                        continue;
                    }

                    notes.Add(clip.name + " " + SfxLoudness.PeakDb(peak).ToString("0.0") + " dB");
                }
            }

            return notes.Count == 0
                ? "조용해서 빼 둔 파일: 없음"
                : "조용해서 빼 둔 파일: " + string.Join(", ", notes);
        }

        static string ImportStatus()
        {
            var bad = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!SfxMetaOk(path))
                {
                    bad.Add(Path.GetFileName(path));
                }
            }

            return bad.Count == 0
                ? "임포트: SFX DecompressOnLoad, WebGL Vorbis 모두 적용됨"
                : "임포트 설정이 다른 파일: " + string.Join(", ", bad);
        }

        static bool SfxMetaOk(string assetPath)
        {
            var metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                return false;
            }

            var text = File.ReadAllText(metaPath);
            var web = text.IndexOf("WebGL:", StringComparison.Ordinal);
            if (web < 0)
            {
                return false;
            }

            var block = text.Substring(web);
            var end = block.IndexOf("\n  forceToMono:", StringComparison.Ordinal);
            if (end > 0)
            {
                block = block.Substring(0, end);
            }

            var defaults = text.Substring(0, web);
            return defaults.Contains("loadType: 0")
                && block.Contains("loadType: 0")
                && block.Contains("compressionFormat: 1");
        }

        static Dictionary<string, List<AudioClip>> IndexClips()
        {
            var map = new Dictionary<string, List<AudioClip>>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    continue;
                }

                var token = FileToken(path);
                if (!map.TryGetValue(token, out var list))
                {
                    list = new List<AudioClip>();
                    map[token] = list;
                }

                list.Add(clip);
            }

            foreach (var list in map.Values)
            {
                list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            }

            return map;
        }

        static AudioClip[] ClipsFor(SfxId id, Dictionary<string, List<AudioClip>> clipsByToken)
        {
            if (!clipsByToken.TryGetValue(IdToken(id), out var list))
            {
                return Array.Empty<AudioClip>();
            }

            var audible = new List<AudioClip>();
            foreach (var clip in list)
            {
                if (clip != null && !SfxLoudness.IsTooQuiet(clip))
                {
                    audible.Add(clip);
                }
            }

            return audible.ToArray();
        }

        static string IdToken(SfxId id)
        {
            return "sfx" + id.ToString().ToLowerInvariant();
        }

        static string FileToken(string path)
        {
            var name = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"[_\s-]*\d+$", "");
            var token = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                if (char.IsLetterOrDigit(name[i]))
                {
                    token.Append(char.ToLowerInvariant(name[i]));
                }
            }

            return token.ToString();
        }
    }
}
