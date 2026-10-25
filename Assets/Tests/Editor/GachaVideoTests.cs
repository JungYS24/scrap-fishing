using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using ScrapFishing.Audio;
using ScrapFishing.Data;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    public class GachaVideoTests
    {
        static readonly string[] Keys = { "lure", "rod", "boat", "drone", "fisher", "helmet" };
        static readonly float[] Flash = { 3.5f, 4.3f, 5.2f, 5.5f, 5.2f, 5.2f };
        static readonly float[] Card = { 6.5f, 7.7f, 7.5f, 7.5f, 7.2f, 7.5f };

        // EnterPlayMode는 도메인을 다시 불러오므로 콜백 결과는 정적 필드에 둔다.
        static int _finished;

        static void OnFinished()
        {
            _finished++;
        }

        [Test]
        public void ConfigAsset_MatchesTableAndFilesExist()
        {
            var config = AssetDatabase.LoadAssetAtPath<GachaVideoConfig>("Assets/Resources/Gacha/GachaVideoConfig.asset");
            Assert.IsNotNull(config, "Resources/Gacha/GachaVideoConfig.asset");
            Assert.AreEqual(6, config.machines.Count);
            for (var i = 0; i < Keys.Length; i++)
            {
                var entry = config.For("machine_" + Keys[i]);
                Assert.IsNotNull(entry, Keys[i]);
                Assert.AreSame(entry, config.For(Keys[i]));
                Assert.AreEqual("gacha_" + Keys[i] + ".mp4", entry.videoFile);
                Assert.AreEqual(Flash[i], entry.flashTime, 0.001f, Keys[i] + " 섬광");
                Assert.AreEqual(Card[i], entry.cardTime, 0.001f, Keys[i] + " 결과 카드");
                Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, "Gacha", entry.videoFile)), entry.videoFile);
                Assert.IsNotNull(entry.sfx, Keys[i] + " 효과음");
                Assert.AreEqual(10f, entry.sfx.length, 0.3f, Keys[i] + " 효과음 길이");
                StringAssert.EndsWith("Gacha" + Path.DirectorySeparatorChar + entry.videoFile, GachaVideoConfig.VideoUrl(entry).Replace('/', Path.DirectorySeparatorChar));
            }

            Assert.AreEqual(0.6f, config.flashDuration, 0.001f);
        }

        [Test]
        public void FlashColor_ByHighestRarity()
        {
            Assert.IsNull(GachaVideoConfig.FlashColor(ItemRarity.Normal), "노멀은 섬광 없음");
            AssertColor("3FA9FF", GachaVideoConfig.FlashColor(ItemRarity.Rare));
            AssertColor("B45CFF", GachaVideoConfig.FlashColor(ItemRarity.Epic));
            AssertColor("FF9A2E", GachaVideoConfig.FlashColor(ItemRarity.Legend));
            AssertColor("FFFFFF", GachaVideoConfig.FlashColor(ItemRarity.Synchro));
            AssertColor("FF5FD2", GachaVideoConfig.SynchroFlashColors[1]);
            AssertColor("B8FF3C", GachaVideoConfig.SynchroFlashColors[2]);

            var gears = new List<GearData>();
            var result = Make(null, 10, ItemRarity.Epic, gears);
            Assert.AreEqual(ItemRarity.Epic, GachaVideoConfig.HighestRarity(result));
            Assert.AreEqual(ItemRarity.Normal, GachaVideoConfig.HighestRarity(Make(null, 10, ItemRarity.Normal, gears)));
            foreach (var gear in gears)
            {
                Object.DestroyImmediate(gear);
            }
        }

        [Test]
        public void Seen_IsSavedPerMachine()
        {
            GachaVideoConfig.ClearSeen("machine_rod");
            GachaVideoConfig.ClearSeen("lure");
            Assert.IsFalse(GachaVideoConfig.HasSeen("rod"));
            GachaVideoConfig.MarkSeen("machine_rod");
            Assert.IsTrue(GachaVideoConfig.HasSeen("rod"), "machine_ 접두어와 관계없이 같은 기록");
            Assert.IsFalse(GachaVideoConfig.HasSeen("machine_lure"), "다른 머신은 따로");
            GachaVideoConfig.ClearSeen("rod");
        }

        static void AssertColor(string hex, Color? color)
        {
            Assert.IsTrue(color.HasValue);
            Assert.AreEqual(hex, ColorUtility.ToHtmlStringRGB(color.Value));
        }

        // 맨 앞 장비가 지정 등급, 나머지는 노멀인 결과를 만든다(섬광은 가장 높은 등급을 따른다).
        static GachaPullResult Make(GachaMachineData machine, int count, ItemRarity top, List<GearData> made)
        {
            var result = new GachaPullResult { Machine = machine, Count = count };
            for (var i = 0; i < count; i++)
            {
                var rarity = i == count / 2 ? top : ItemRarity.Normal;
                var gear = GearData.Create("test_" + rarity + "_" + i, rarity + " " + i, GearSlot.Lure, rarity);
                made.Add(gear);
                result.Items.Add(new PulledGear(gear, false));
            }

            return result;
        }

        static GameObject MakeArea(out RectTransform area)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas.transform, false);
            area.sizeDelta = new Vector2(480f, 854f);
            return canvas;
        }

        static float Brightness(RenderTexture texture)
        {
            var active = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.active = active;
            var pixels = read.GetPixels32();
            Object.DestroyImmediate(read);
            long sum = 0;
            var samples = 0;
            for (var i = 0; i < pixels.Length; i += 97)
            {
                sum += pixels[i].r + pixels[i].g + pixels[i].b;
                samples++;
            }

            return sum / (samples * 3f * 255f);
        }

        static void Tap(GachaVideoView view)
        {
            view.GetComponent<Button>().onClick.Invoke();
        }

        // 머신 6종: 처음 보는 1연은 끝까지(건너뛰기 불가) → 섬광·결과 카드 시점을 영상 시간으로 확인하고
        // 그 순간 영상이 흰 화면인지 본다. 이어서 10연은 본 기록이 있어 탭 한 번에 결과 카드로 넘어간다.
        [UnityTest]
        public IEnumerator EveryMachine_FlashAndCardsFollowVideoTime()
        {
            yield return new EnterPlayMode();
            var canvas = MakeArea(out var area);
            var config = GachaVideoConfig.Load();
            var view = GachaVideoView.Create(area, config);
            var made = new List<GearData>();
            var tops = new[] { ItemRarity.Normal, ItemRarity.Epic, ItemRarity.Legend, ItemRarity.Synchro, ItemRarity.Rare, ItemRarity.Legend };
            var report = new StringBuilder("오리파 연출 영상 확인\n");
            for (var i = 0; i < Keys.Length; i++)
            {
                var machine = ScriptableObject.CreateInstance<GachaMachineData>();
                machine.Configure("machine_" + Keys[i], Keys[i], GearSlot.Lure, 100, new List<GearData>());
                GachaVideoConfig.ClearSeen(machine.Id);
                view.Prepare(machine);
                var prepareStart = Time.realtimeSinceStartup;
                while (!view.Player.isPrepared && Time.realtimeSinceStartup - prepareStart < 10f)
                {
                    yield return null;
                }

                Assert.IsTrue(view.Player.isPrepared, Keys[i] + " 영상 준비");

                _finished = 0;
                var one = Make(machine, 1, tops[i], made);
                Assert.IsTrue(view.Play(one, OnFinished), Keys[i] + " 1연 재생");
                Assert.IsFalse(view.CanSkip, "처음 보는 영상은 건너뛸 수 없다");
                Assert.IsTrue(view.Audio.isPlaying, Keys[i] + " 미리 불러 둔 영상은 소리를 바로 시작");
                Tap(view);
                Assert.IsFalse(view.ShowingCards, "처음 보는 영상은 탭해도 넘어가지 않는다");

                var timeline = new StringBuilder();
                var nextSample = 0d;
                var flashAt = -1d;
                var cardAt = -1d;
                var drift = 0f;
                var start = Time.realtimeSinceStartup;
                while (!view.ShowingCards && Time.realtimeSinceStartup - start < 20f)
                {
                    var t = view.VideoTime;
                    if (t >= nextSample)
                    {
                        timeline.Append(t.ToString("0.00")).Append('=').Append(Brightness(view.Texture).ToString("0.00")).Append(' ');
                        nextSample += 0.25d;
                    }

                    if (view.Audio.isPlaying && t > 0.5d)
                    {
                        drift = Mathf.Max(drift, Mathf.Abs(view.Audio.time - (float)t));
                    }

                    if (flashAt < 0d && view.Flashed)
                    {
                        flashAt = t;
                    }

                    yield return null;
                }

                cardAt = view.VideoTime;
                var white = Brightness(view.Texture);
                Assert.IsTrue(view.ShowingCards, Keys[i] + " 결과 카드");
                Assert.AreEqual(1, view.CardCount, Keys[i] + " 1연 카드 1장");
                Assert.GreaterOrEqual(flashAt, Flash[i] - 0.001d, Keys[i] + " 섬광 시점");
                Assert.Less(flashAt, Flash[i] + 0.3d, Keys[i] + " 섬광 시점");
                Assert.GreaterOrEqual(cardAt, Card[i] - 0.001d, Keys[i] + " 결과 카드 시점");
                Assert.Less(cardAt, Card[i] + 0.3d, Keys[i] + " 결과 카드 시점");
                Assert.AreEqual(tops[i], view.LastFlash, Keys[i] + " 섬광 등급");
                Assert.Less(drift, 0.25f, Keys[i] + " 소리·영상 어긋남");
                report.AppendFormat("{0}: 섬광 {1:0.00}s, 카드 {2:0.00}s, 카드 시점 영상 밝기 {3:0.00}, 소리 최대 어긋남 {4:0.000}s, 섬광 등급 {5}\n  밝기 {6}\n",
                    Keys[i], flashAt, cardAt, white, drift, view.LastFlash, timeline);

                while (!view.Revealed)
                {
                    yield return null;
                }

                Tap(view);
                Assert.AreEqual(1, _finished, Keys[i] + " 결과 카드 뒤 탭으로 닫힘");
                Assert.IsFalse(view.IsPlaying);
                Assert.IsTrue(GachaVideoConfig.HasSeen(machine.Id), Keys[i] + " 본 기록");

                yield return new WaitForSecondsRealtime(0.3f);
                var ten = Make(machine, 10, tops[i], made);
                Assert.IsTrue(view.Play(ten, OnFinished), Keys[i] + " 10연 재생");
                Assert.IsTrue(view.CanSkip, Keys[i] + " 두 번째부터 건너뛰기");
                Assert.IsTrue(view.transform.Find("Skip").gameObject.activeSelf, "SKIP 표시");
                yield return new WaitForSecondsRealtime(0.5f);
                Tap(view);
                Assert.IsTrue(view.ShowingCards, Keys[i] + " 탭 한 번에 결과 카드");
                Assert.IsFalse(view.transform.Find("Skip").gameObject.activeSelf, "결과 카드에서는 SKIP 숨김");
                Assert.AreEqual(10, view.CardCount, Keys[i] + " 10연 카드 10장");
                Assert.IsFalse(view.Revealed, "카드는 차례로 뒤집힌다");
                Tap(view);
                Assert.AreEqual(1, _finished, "다 뒤집히기 전 탭은 무시");
                while (!view.Revealed)
                {
                    yield return null;
                }

                Tap(view);
                Assert.AreEqual(2, _finished);
                GachaVideoConfig.ClearSeen(machine.Id);
                Object.DestroyImmediate(machine);
            }

            Debug.Log(report.ToString());
            Object.DestroyImmediate(canvas);
            foreach (var gear in made)
            {
                Object.DestroyImmediate(gear);
            }

            yield return new ExitPlayMode();
        }

        // 영상 파일이 없으면 연출 없이 곧바로 결과로 넘어간다(멈추지 않는다).
        [UnityTest]
        public IEnumerator MissingVideo_GoesStraightToResults()
        {
            yield return new EnterPlayMode();
            // VideoPlayer가 직접 남기는 "cannot play url" 오류 로그는 예상된 것이다.
            LogAssert.ignoreFailingMessages = true;
            var canvas = MakeArea(out var area);
            var config = GachaVideoConfig.CreateDefault();
            config.machines[0].videoFile = "gacha_missing.mp4";
            var view = GachaVideoView.Create(area, config);
            var machine = ScriptableObject.CreateInstance<GachaMachineData>();
            machine.Configure("machine_lure", "lure", GearSlot.Lure, 100, new List<GearData>());
            var made = new List<GearData>();
            _finished = 0;
            var started = view.Play(Make(machine, 10, ItemRarity.Legend, made), OnFinished);
            var start = Time.realtimeSinceStartup;
            while (started && _finished == 0 && Time.realtimeSinceStartup - start < 6f)
            {
                yield return null;
            }

            var waited = Time.realtimeSinceStartup - start;
            Assert.IsTrue(!started || _finished == 1, "없는 영상은 결과로 넘어간다");
            Assert.Less(waited, 2f, "오류를 받자마자 넘어간다");
            Assert.IsFalse(view.IsPlaying);
            Assert.AreEqual(0f, view.GetComponent<CanvasGroup>().alpha);
            Debug.Log("없는 영상: Play=" + started + ", 결과까지 " + waited.ToString("0.00") + "s");

            // 다음 뽑기도 같은 방식으로 바로 넘어간다.
            _finished = 0;
            var again = view.Play(Make(machine, 1, ItemRarity.Normal, made), OnFinished);
            start = Time.realtimeSinceStartup;
            while (again && _finished == 0 && Time.realtimeSinceStartup - start < 6f)
            {
                yield return null;
            }

            Assert.IsTrue(!again || _finished == 1);
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(machine);
            foreach (var gear in made)
            {
                Object.DestroyImmediate(gear);
            }

            yield return new ExitPlayMode();
        }

        // 음소거면 연출 소리도 꺼지고, 효과음 음량을 따른다.
        [UnityTest]
        public IEnumerator Sound_FollowsMuteAndSfxVolume()
        {
            yield return new EnterPlayMode();
            var audio = AudioManager.Ensure();
            var soundBefore = audio.SoundOn;
            var sfxBefore = VolumeSettings.Get(VolumeChannel.Sfx);
            var canvas = MakeArea(out var area);
            var view = GachaVideoView.Create(area);
            var machine = ScriptableObject.CreateInstance<GachaMachineData>();
            machine.Configure("machine_drone", "drone", GearSlot.Drone, 100, new List<GearData>());
            var made = new List<GearData>();
            try
            {
                audio.SetSoundOn(false);
                Assert.IsTrue(view.Play(Make(machine, 1, ItemRarity.Rare, made), OnFinished));
                Assert.IsTrue(view.Audio.mute, "음소거");
                Assert.AreEqual(0f, AudioListener.volume, "전체 소리도 꺼져 있다");
                audio.SetSoundOn(true);
                Assert.IsFalse(view.Audio.mute, "음소거를 풀면 바로 따라온다");
                VolumeSettings.Set(VolumeChannel.Sfx, 20, false);
                Assert.AreEqual(audio.SfxLevel, view.Audio.volume, 0.001f, "효과음 음량");
                VolumeSettings.Set(VolumeChannel.Sfx, 100, false);
                Assert.AreEqual(audio.SfxLevel, view.Audio.volume, 0.001f, "효과음 음량");
                Assert.Greater(view.Audio.volume, 0f);
                yield return null;
            }
            finally
            {
                VolumeSettings.Set(VolumeChannel.Sfx, sfxBefore, false);
                audio.SetSoundOn(soundBefore);
                view.HideNow();
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(machine);
                foreach (var gear in made)
                {
                    Object.DestroyImmediate(gear);
                }
            }

            yield return new ExitPlayMode();
        }
    }
}
