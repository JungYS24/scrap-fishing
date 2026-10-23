using System;
using UnityEngine;

namespace ScrapFishing.Audio
{
    public enum VolumeChannel
    {
        Master,
        Bgm,
        Sfx
    }

    // 설정창의 음량 3종. 정수 0~100으로 PlayerPrefs에 저장하고, 기본값 70이 기존 소리(1.0배)다.
    // 배수 = 값 / 70 이라서 100이면 약 1.43배, 0이면 무음. 음소거(SoundToggle)와는 따로 동작한다.
    public static class VolumeSettings
    {
        public const string MasterKey = "vol_master";
        public const string BgmKey = "vol_bgm";
        public const string SfxKey = "vol_sfx";
        public const int DefaultValue = 70;
        public const int MaxValue = 100;

        static readonly int[] Values = new int[3];
        static bool _loaded;

        public static event Action Changed;

        public static float SfxGain => Gain(VolumeChannel.Master) * Gain(VolumeChannel.Sfx);
        public static float BgmGain => Gain(VolumeChannel.Master) * Gain(VolumeChannel.Bgm);

        public static int Get(VolumeChannel channel)
        {
            EnsureLoaded();
            return Values[(int)channel];
        }

        // save가 false면 메모리와 PlayerPrefs 값만 바꾸고 디스크에는 쓰지 않는다(슬라이더를 끄는 동안).
        public static void Set(VolumeChannel channel, int value, bool save = true)
        {
            EnsureLoaded();
            value = Mathf.Clamp(value, 0, MaxValue);
            var index = (int)channel;
            var changed = Values[index] != value;
            if (changed)
            {
                Values[index] = value;
                PlayerPrefs.SetInt(Key(channel), value);
            }

            if (save)
            {
                Save();
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public static void ResetDefaults()
        {
            EnsureLoaded();
            for (var i = 0; i < Values.Length; i++)
            {
                Values[i] = DefaultValue;
                PlayerPrefs.SetInt(Key((VolumeChannel)i), DefaultValue);
            }

            Save();
            Changed?.Invoke();
        }

        public static float Multiplier(int value)
        {
            return Mathf.Clamp(value, 0, MaxValue) / (float)DefaultValue;
        }

        public static float Gain(VolumeChannel channel)
        {
            return Multiplier(Get(channel));
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        // PlayerPrefs에서 다시 읽는다. 테스트와 앱 재시작 흉내에 쓴다.
        public static void Reload()
        {
            _loaded = false;
            EnsureLoaded();
            Changed?.Invoke();
        }

        public static string Key(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Bgm: return BgmKey;
                case VolumeChannel.Sfx: return SfxKey;
                default: return MasterKey;
            }
        }

        static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            for (var i = 0; i < Values.Length; i++)
            {
                Values[i] = Mathf.Clamp(PlayerPrefs.GetInt(Key((VolumeChannel)i), DefaultValue), 0, MaxValue);
            }
        }
    }
}
