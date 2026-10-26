using System;
using System.Collections.Generic;
using System.IO;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // 오리파 뽑기 연출 영상 설정. 머신마다 영상 파일(StreamingAssets/Gacha), 같은 길이의 효과음, 등급 섬광·결과 카드 시점을 둔다.
    // Resources/Gacha/GachaVideoConfig.asset을 Inspector에서 고치면 되고, 에셋이 없으면 CreateDefault 값으로 돈다.
    [CreateAssetMenu(menuName = "ScrapFishing/Gacha/GachaVideoConfig")]
    public class GachaVideoConfig : ScriptableObject
    {
        public const string Resource = "Gacha/GachaVideoConfig";
        public const string VideoFolder = "Gacha";
        public const string SfxFolder = "Audio/Gacha/";
        public const int MaxVideoPull = 10;
        const string SeenPrefix = "gacha_video_seen_";
        const string MachinePrefix = "machine_";

        [Serializable]
        public class Entry
        {
            public string machineId;
            [Tooltip("StreamingAssets/Gacha 아래 파일 이름")]
            public string videoFile;
            [Tooltip("영상과 같은 길이의 효과음(Resources/Audio/Gacha)")]
            public AudioClip sfx;
            [Tooltip("이번 뽑기의 최고 등급 색으로 화면이 번쩍이는 시점(초)")]
            public float flashTime;
            [Tooltip("영상이 흰 화면이 된 뒤 결과 카드를 띄우는 시점(초)")]
            public float cardTime;
        }

        public List<Entry> machines = new List<Entry>();

        [Header("등급 섬광")]
        public float flashDuration = 0.6f;
        [Range(0f, 1f)] public float flashAlpha = 0.85f;
        public float legendShakeStrength = 14f;
        public float legendShakeDuration = 0.45f;
        [Tooltip("레전드 섬광 때 함께 낼 효과음(비워 두면 없음)")]
        public AudioClip legendFlashSfx;
        [Tooltip("싱크로 섬광 때 효과음. 비워 두면 레전드 효과음을 쓴다.")]
        public AudioClip synchroFlashSfx;

        [Header("결과 카드")]
        [Tooltip("10연 격자의 열 수(2면 2열 × 5행)")]
        [Min(1)] public int tenColumns = 2;
        public float cardFlipInterval = 0.1f;
        public float cardFlipDuration = 0.2f;

        // 결과 카드 뒤 사이키델릭 파장(ScrapFishing/UI/PsyWaves). 이번 뽑기 최고 등급의 색을 쓴다.
        [Serializable]
        public class PsyPalette
        {
            public Color a = Color.black;
            public Color b = Color.gray;
            public Color c = Color.white;
            [Tooltip("흐름 속도(셰이더 _Speed)")] public float speed = 0.6f;
            [Range(0f, 1f)] public float intensity = 1f;
            [Tooltip("색 순환 속도 배율")] public float cycle = 1f;
        }

        [Header("결과 배경 파장 (노멀·레어·에픽·레전드·싱크로 순)")]
        public PsyPalette[] psyPalettes = DefaultPalettes();
        public float psyFrequency = 36f;
        public float psySwirl = 6f;
        [Range(0f, 1f)] public float psyCenterDarken = 0.35f;
        [Tooltip("흰 화면에서 파장으로 바뀌는 시간(초)")]
        public float psyFadeIn = 0.4f;
        [Tooltip("레전드·싱크로: 처음 이 시간 동안 속도·세기를 잠깐 올린다")]
        public float psyPulseDuration = 0.3f;
        public float psyPulseSpeed = 2.5f;
        public float psyPulseIntensity = 0.3f;
        [Tooltip("렌더 해상도 = 화면 / 이 값(Point 확대로 픽셀아트 느낌)")]
        [Min(1)] public int psyDownscale = 4;

        [Header("안전장치")]
        [Tooltip("재생을 눌렀는데 이 시간 안에 영상이 시작되지 않으면 연출 없이 결과로 넘어간다")]
        public float startTimeout = 4f;
        [Tooltip("재생 중 영상 시간이 이만큼 멈춰 있으면 결과로 넘어간다")]
        public float stallTimeout = 3f;

        static readonly (string id, float flash, float card)[] Defaults =
        {
            ("lure", 3.5f, 6.5f),
            ("rod", 4.3f, 7.7f),
            ("boat", 5.2f, 7.5f),
            ("drone", 5.5f, 7.5f),
            ("fisher", 5.2f, 7.2f),
            ("helmet", 5.2f, 7.5f),
        };

        public static GachaVideoConfig Load()
        {
            var config = Resources.Load<GachaVideoConfig>(Resource);
            return config != null ? config : CreateDefault();
        }

        public static GachaVideoConfig CreateDefault()
        {
            var config = CreateInstance<GachaVideoConfig>();
            config.name = "GachaVideoConfig";
            foreach (var (id, flash, card) in Defaults)
            {
                config.machines.Add(new Entry
                {
                    machineId = id,
                    videoFile = "gacha_" + id + ".mp4",
                    sfx = Resources.Load<AudioClip>(SfxPath(id)),
                    flashTime = flash,
                    cardTime = card,
                });
            }

            return config;
        }

        public static string SfxPath(string machineId)
        {
            return SfxFolder + "gacha_" + KeyOf(machineId) + "_sfx";
        }

        // 카탈로그의 머신 id(machine_lure)와 설정 키(lure) 둘 다 받는다.
        public static string KeyOf(string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return string.Empty;
            }

            return machineId.StartsWith(MachinePrefix, StringComparison.Ordinal) ? machineId.Substring(MachinePrefix.Length) : machineId;
        }

        public Entry For(string machineId)
        {
            var key = KeyOf(machineId);
            foreach (var entry in machines)
            {
                if (entry != null && KeyOf(entry.machineId) == key)
                {
                    return entry;
                }
            }

            return null;
        }

        public static string VideoUrl(Entry entry)
        {
            return entry == null || string.IsNullOrEmpty(entry.videoFile)
                ? null
                : Path.Combine(Application.streamingAssetsPath, VideoFolder, entry.videoFile);
        }

        public AudioClip FlashSfx(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Synchro && synchroFlashSfx != null)
            {
                return synchroFlashSfx;
            }

            return rarity >= ItemRarity.Legend ? legendFlashSfx : null;
        }

        public static ItemRarity HighestRarity(GachaPullResult result)
        {
            var best = ItemRarity.Normal;
            if (result == null)
            {
                return best;
            }

            foreach (var item in result.Items)
            {
                if (item.Gear != null && item.Gear.Rarity > best)
                {
                    best = item.Gear.Rarity;
                }
            }

            return best;
        }

        // 노멀은 섬광이 없다(null). 싱크로는 하양·핑크·연두를 번갈아 쓰므로 첫 색만 돌려준다.
        public static Color? FlashColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return new Color(0x3F / 255f, 0xA9 / 255f, 1f);
                case ItemRarity.Epic: return new Color(0xB4 / 255f, 0x5C / 255f, 1f);
                case ItemRarity.Legend: return new Color(1f, 0x9A / 255f, 0x2E / 255f);
                case ItemRarity.Synchro: return Color.white;
                default: return null;
            }
        }

        public static PsyPalette[] DefaultPalettes()
        {
            return new[]
            {
                Palette("2A3350", "5A6680", "9AA3B5", 0.6f, 0.5f, 1f),
                Palette("0E2A66", "3FA9FF", "9FE3FF", 0.6f, 1f, 1f),
                Palette("2A0E5E", "B45CFF", "FF8AF0", 0.6f, 1f, 1f),
                Palette("5A1E00", "FF9A2E", "FFE08A", 0.9f, 1f, 1f),
                Palette("FFFFFF", "FF5FD2", "B8FF3C", 1.1f, 1f, 2.2f),
            };
        }

        static PsyPalette Palette(string a, string b, string c, float speed, float intensity, float cycle)
        {
            return new PsyPalette { a = Hex(a), b = Hex(b), c = Hex(c), speed = speed, intensity = intensity, cycle = cycle };
        }

        static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;
        }

        public PsyPalette PaletteFor(ItemRarity rarity)
        {
            var palettes = psyPalettes != null && psyPalettes.Length >= ItemRarityCount ? psyPalettes : DefaultPalettes();
            return palettes[Mathf.Clamp((int)rarity, 0, palettes.Length - 1)];
        }

        const int ItemRarityCount = 5;

        public static readonly Color[] SynchroFlashColors =
        {
            Color.white,
            new Color(1f, 0x5F / 255f, 0xD2 / 255f),
            new Color(0xB8 / 255f, 1f, 0x3C / 255f),
        };

        // 같은 머신 영상을 한 번 끝까지(결과 카드 시점까지) 본 기록. 본 영상만 탭으로 건너뛸 수 있다.
        public static bool HasSeen(string machineId)
        {
            return PlayerPrefs.GetInt(SeenPrefix + KeyOf(machineId), 0) != 0;
        }

        public static void MarkSeen(string machineId)
        {
            if (HasSeen(machineId))
            {
                return;
            }

            PlayerPrefs.SetInt(SeenPrefix + KeyOf(machineId), 1);
            PlayerPrefs.Save();
        }

        public static void ClearSeen(string machineId)
        {
            PlayerPrefs.DeleteKey(SeenPrefix + KeyOf(machineId));
        }
    }
}
