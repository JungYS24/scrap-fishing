using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ScrapFishing.Data;
using ScrapFishing.Gacha;
using UnityEngine;
using UnityEngine.TestTools;

namespace ScrapFishing.Tests
{
    // 뽑기 결과 카드 뒤 사이키델릭 파장: 등급별 색, 흰색→파장 전환, 레전드·싱크로 펄스, 숨기면 렌더 중지.
    public class PsyWavesTests
    {
        [Test]
        public void Shader_AndDefaultPalettes()
        {
            var shader = Resources.Load<Shader>(PsyWavesBackground.ShaderResource);
            Assert.IsNotNull(shader, "Resources/Shaders/PsyWaves");
            Assert.AreEqual("ScrapFishing/UI/PsyWaves", shader.name);
            foreach (var property in new[] { "_ColorA", "_ColorB", "_ColorC", "_Speed", "_Frequency", "_Swirl", "_Intensity", "_CenterDarken", "_PixelSize", "_WhiteFade", "_Stencil", "_ColorMask" })
            {
                Assert.GreaterOrEqual(shader.FindPropertyIndex(property), 0, property);
            }

            var palettes = GachaVideoConfig.DefaultPalettes();
            AssertPalette(palettes[0], "2A3350", "5A6680", "9AA3B5");
            Assert.AreEqual(0.5f, palettes[0].intensity, 1e-4f, "노멀은 차분하게");
            AssertPalette(palettes[1], "0E2A66", "3FA9FF", "9FE3FF");
            AssertPalette(palettes[2], "2A0E5E", "B45CFF", "FF8AF0");
            AssertPalette(palettes[3], "5A1E00", "FF9A2E", "FFE08A");
            Assert.AreEqual(0.9f, palettes[3].speed, 1e-4f);
            AssertPalette(palettes[4], "FFFFFF", "FF5FD2", "B8FF3C");
            Assert.AreEqual(1.1f, palettes[4].speed, 1e-4f);
            Assert.Greater(palettes[4].cycle, palettes[3].cycle, "싱크로는 색 순환이 더 빠르다");
            var config = GachaVideoConfig.Load();
            Assert.AreEqual(5, config.psyPalettes.Length, "설정 에셋에서 고칠 수 있다");
        }

        static void AssertPalette(GachaVideoConfig.PsyPalette palette, string a, string b, string c)
        {
            Assert.AreEqual(a, ColorUtility.ToHtmlStringRGB(palette.a));
            Assert.AreEqual(b, ColorUtility.ToHtmlStringRGB(palette.b));
            Assert.AreEqual(c, ColorUtility.ToHtmlStringRGB(palette.c));
        }

        static GameObject MakeArea(out RectTransform area)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas.transform, false);
            area.sizeDelta = new Vector2(480f, 854f);
            return canvas;
        }

        static GachaPullResult Result(GachaMachineData machine, int count, ItemRarity top, List<GearData> made)
        {
            var result = new GachaPullResult { Machine = machine, Count = count };
            for (var i = 0; i < count; i++)
            {
                var rarity = i == count / 2 ? top : ItemRarity.Normal;
                var gear = GearData.Create("psy_" + rarity + "_" + i, rarity.ToString(), GearSlot.Lure, rarity);
                made.Add(gear);
                result.Items.Add(new PulledGear(gear, false));
            }

            return result;
        }

        // 연출 영상 없이 결과 카드 단계만 띄운다(카드가 나타나는 순간 = 배경 시작).
        static void ShowCards(GachaVideoView view, GachaPullResult result)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GachaVideoView).GetField("_result", flags).SetValue(view, result);
            typeof(GachaVideoView).GetMethod("ShowOverlay", flags).Invoke(view, null);
            typeof(GachaVideoView).GetMethod("ShowCards", flags).Invoke(view, new object[] { false });
        }

        static Color Average(RenderTexture texture)
        {
            var active = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.active = active;
            var pixels = read.GetPixels();
            Object.DestroyImmediate(read);
            var sum = Color.clear;
            foreach (var pixel in pixels)
            {
                sum += pixel;
            }

            return sum / pixels.Length;
        }

        [UnityTest]
        public IEnumerator EveryRarity_FadesFromWhiteIntoItsPalette()
        {
            yield return new EnterPlayMode();
            var canvas = MakeArea(out var area);
            var config = GachaVideoConfig.Load();
            var view = GachaVideoView.Create(area, config);
            var machine = ScriptableObject.CreateInstance<GachaMachineData>();
            machine.Configure("machine_lure", "lure", GearSlot.Lure, 100, new List<GearData>());
            var made = new List<GearData>();
            var dir = System.Environment.GetEnvironmentVariable("SNAP_DIR");
            try
            {
                Assert.IsNotNull(view.Background, "셰이더를 쓸 수 있다");
                foreach (var count in new[] { 1, 10 })
                {
                    foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
                    {
                        ShowCards(view, Result(machine, count, rarity, made));
                        var background = view.Background;
                        Assert.AreEqual(rarity, background.Rarity, count + "연 " + rarity + " 최고 등급 색");
                        Assert.AreSame(config.PaletteFor(rarity), background.Palette);
                        var colorA = background.Material.GetColor("_ColorA");
                        Assert.AreEqual(background.Palette.a.r, colorA.r, 0.01f);
                        Assert.AreEqual(background.Palette.a.b, colorA.b, 0.01f);
                        Assert.IsTrue(background.Rendering);

                        // 흰색 → 파장: 처음엔 흰색, 0.4초 동안 끊김 없이(뒤로 가지 않고) 올라간다.
                        Assert.AreEqual(0f, background.Fade, 0.05f, "카드가 나타나는 순간은 흰색");
                        var white = Average(background.Texture);
                        Assert.Greater(white.r + white.g + white.b, 2.85f, "처음은 영상의 흰 화면과 같은 흰색");
                        var startSpeed = background.CurrentSpeed;
                        var last = background.Fade;
                        var start = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - start < 0.55f)
                        {
                            yield return null;
                            Assert.GreaterOrEqual(background.Fade, last, "전환이 뒤로 가지 않는다");
                            Assert.LessOrEqual(background.Fade - last, 0.5f, "한 프레임에 크게 튀지 않는다");
                            last = background.Fade;
                        }

                        Assert.AreEqual(1f, background.Fade, 1e-4f);
                        if (rarity >= ItemRarity.Legend)
                        {
                            Assert.Greater(startSpeed, background.Palette.speed * 1.5f, rarity + " 시작 펄스");
                        }

                        Assert.AreEqual(background.Palette.speed, background.CurrentSpeed, 1e-3f, "펄스 뒤 원래 속도");
                        Assert.AreEqual(background.Palette.intensity, background.CurrentIntensity, 1e-3f);

                        // 그려진 색이 그 등급 팔레트 쪽이다(팔레트 평균과 가깝고 흰색과는 멀다. 싱크로는 하양이 섞여 밝다).
                        var average = Average(background.Texture);
                        var palette = (background.Palette.a + background.Palette.b + background.Palette.c) / 3f;
                        var distance = Mathf.Abs(average.r - palette.r) + Mathf.Abs(average.g - palette.g) + Mathf.Abs(average.b - palette.b);
                        Assert.Less(distance, 0.6f, count + "연 " + rarity + " 색: " + average + " / 팔레트 평균 " + palette);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Save(background.Texture, Path.Combine(dir, "psy_" + count + "_" + rarity + ".png"));
                        }

                        view.HideNow();
                        Assert.IsFalse(background.gameObject.activeInHierarchy, "결과 화면이 닫히면 배경도 끈다");
                        Assert.IsFalse(background.Rendering, "숨겨진 동안 렌더하지 않는다");
                        Assert.IsNull(background.Texture, "RT도 놓는다");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(machine);
                foreach (var gear in made)
                {
                    Object.DestroyImmediate(gear);
                }
            }

            yield return new ExitPlayMode();
        }

        static void Save(RenderTexture texture, string path)
        {
            var active = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.active = active;
            File.WriteAllBytes(path, read.EncodeToPNG());
            Object.DestroyImmediate(read);
        }
    }
}
