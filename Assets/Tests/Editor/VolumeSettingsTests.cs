using NUnit.Framework;
using ScrapFishing.Audio;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class VolumeSettingsTests
    {
        static readonly VolumeChannel[] Channels = { VolumeChannel.Master, VolumeChannel.Bgm, VolumeChannel.Sfx };

        readonly int?[] _saved = new int?[3];

        // 테스트가 실제 PlayerPrefs를 건드리므로 원래 값을 기억했다가 되돌린다.
        [SetUp]
        public void SetUp()
        {
            for (var i = 0; i < Channels.Length; i++)
            {
                var key = VolumeSettings.Key(Channels[i]);
                _saved[i] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }

            VolumeSettings.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < Channels.Length; i++)
            {
                var key = VolumeSettings.Key(Channels[i]);
                if (_saved[i].HasValue)
                {
                    PlayerPrefs.SetInt(key, _saved[i].Value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(key);
                }
            }

            PlayerPrefs.Save();
            VolumeSettings.Reload();
        }

        [Test]
        public void Multiplier_IsValueOver70()
        {
            Assert.AreEqual(0f, VolumeSettings.Multiplier(0), 1e-6f);
            Assert.AreEqual(1f, VolumeSettings.Multiplier(70), 1e-6f, "70이면 지금과 같은 소리");
            Assert.AreEqual(100f / 70f, VolumeSettings.Multiplier(100), 1e-6f);
            Assert.AreEqual(100f / 70f, VolumeSettings.Multiplier(150), 1e-6f, "100을 넘으면 100으로 자른다");
            Assert.AreEqual(0f, VolumeSettings.Multiplier(-5), 1e-6f);
        }

        [Test]
        public void Defaults_Are70AndUnityGain()
        {
            foreach (var channel in Channels)
            {
                Assert.AreEqual(70, VolumeSettings.Get(channel), channel.ToString());
            }

            Assert.AreEqual(1f, VolumeSettings.SfxGain, 1e-6f);
            Assert.AreEqual(1f, VolumeSettings.BgmGain, 1e-6f);
        }

        [Test]
        public void SetAndReload_KeepsValuesInPrefs()
        {
            VolumeSettings.Set(VolumeChannel.Master, 100);
            VolumeSettings.Set(VolumeChannel.Bgm, 35);
            VolumeSettings.Set(VolumeChannel.Sfx, 0);
            Assert.AreEqual(100, PlayerPrefs.GetInt(VolumeSettings.MasterKey));
            Assert.AreEqual(35, PlayerPrefs.GetInt(VolumeSettings.BgmKey));
            Assert.AreEqual(0, PlayerPrefs.GetInt(VolumeSettings.SfxKey));

            VolumeSettings.Reload();
            Assert.AreEqual(100, VolumeSettings.Get(VolumeChannel.Master));
            Assert.AreEqual(35, VolumeSettings.Get(VolumeChannel.Bgm));
            Assert.AreEqual(0, VolumeSettings.Get(VolumeChannel.Sfx));
            Assert.AreEqual(100f / 70f * 0.5f, VolumeSettings.BgmGain, 1e-5f, "전체 × 배경음");
            Assert.AreEqual(0f, VolumeSettings.SfxGain, 1e-6f);
        }

        [Test]
        public void Set_ClampsAndRaisesChanged()
        {
            var raised = 0;
            void Count() => raised++;
            VolumeSettings.Changed += Count;
            try
            {
                VolumeSettings.Set(VolumeChannel.Sfx, 250, false);
                Assert.AreEqual(100, VolumeSettings.Get(VolumeChannel.Sfx));
                VolumeSettings.Set(VolumeChannel.Sfx, 100, false);
                Assert.AreEqual(1, raised, "같은 값이면 다시 알리지 않는다");
            }
            finally
            {
                VolumeSettings.Changed -= Count;
            }
        }

        [Test]
        public void ResetDefaults_SetsAllTo70()
        {
            VolumeSettings.Set(VolumeChannel.Master, 10);
            VolumeSettings.Set(VolumeChannel.Bgm, 90);
            VolumeSettings.Set(VolumeChannel.Sfx, 55);
            VolumeSettings.ResetDefaults();
            VolumeSettings.Reload();
            foreach (var channel in Channels)
            {
                Assert.AreEqual(70, VolumeSettings.Get(channel), channel.ToString());
            }
        }
    }
}
