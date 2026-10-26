using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ScrapFishing.Audio;
using ScrapFishing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    public class SettingsSkinTests
    {
        static readonly string[] Files =
        {
            "panel", "header_bar", "btn_close", "btn_sound_on", "btn_sound_off", "label_box", "slider_track", "slider_fill", "slider_knob",
            "btn_wide_cyan", "btn_wide_gray", "btn_wide_yellow", "btn_wide_green", "footer_box"
        };

        [Test]
        public void Sprites_ImportedWithBordersAndNoCompression()
        {
            foreach (var name in Files)
            {
                var path = SettingsUiImportPostprocessor.Root + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, name);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, name);
                Assert.AreEqual(100f, importer.spritePixelsPerUnit, name);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode, name);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, name);
                Assert.IsFalse(importer.mipmapEnabled, name);
                Assert.IsTrue(importer.alphaIsTransparency, name);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, name);
                var webgl = importer.GetPlatformTextureSettings("WebGL");
                Assert.IsTrue(webgl.overridden, name + " WebGL");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, webgl.textureCompression, name + " WebGL");
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, name);

                var expected = SettingsUiImportPostprocessor.BordersLTRB.TryGetValue(name, out var ltrb)
                    ? new Vector4(ltrb[0], ltrb[3], ltrb[2], ltrb[1])
                    : Vector4.zero;
                Assert.AreEqual(expected, importer.spriteBorder, name + " 테두리(L,B,R,T)");
            }

            // [L,T,R,B] = [10,8,10,8] → Unity (L,B,R,T) = (10,8,10,8). 상하가 다른 값이 없어서 순서를 하나 더 직접 확인한다.
            Assert.AreEqual(new Vector4(1, 4, 3, 2), SettingsUiImportPostprocessor.UnityBorder(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void SkinAsset_HasAllSprites()
        {
            var skin = AssetDatabase.LoadAssetAtPath<SettingsSkin>("Assets/Resources/UI/SettingsSkin.asset");
            Assert.IsNotNull(skin);
            Assert.IsTrue(skin.Complete);
            Assert.AreEqual(new Vector2(522f, 770f), skin.panel.rect.size);
            Assert.AreEqual(new Vector4(130f, 130f, 130f, 130f), skin.panel.border);
        }

        static GameObject MakeArea(out RectTransform area)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas.transform, false);
            area.sizeDelta = new Vector2(480f, 854f);
            return canvas;
        }

        static RectTransform Find(Transform root, string path)
        {
            var found = root.Find(path);
            Assert.IsNotNull(found, path);
            return (RectTransform)found;
        }

        static Rect World(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator Panel_UsesSkinLayout()
        {
            yield return new EnterPlayMode();
            var audio = AudioManager.Ensure();
            var soundBefore = audio.SoundOn;
            var volumes = new Dictionary<VolumeChannel, int>();
            foreach (VolumeChannel channel in System.Enum.GetValues(typeof(VolumeChannel)))
            {
                volumes[channel] = VolumeSettings.Get(channel);
            }

            var canvas = MakeArea(out var area);
            var view = canvas.AddComponent<SettingsView>();
            try
            {
                view.Build(area);
                view.Open();
                yield return new WaitForSecondsRealtime(0.35f);
                Canvas.ForceUpdateCanvases();
                var root = area.Find("Settings");
                var panel = Find(root, "Panel");
                var skin = SettingsSkin.Load();

                // 패널 크기·위치는 그대로(0.06~0.94, 0.14~0.86), 그림은 9-slice.
                Assert.AreEqual(new Vector2(0.06f, 0.14f), panel.anchorMin);
                Assert.AreEqual(new Vector2(0.94f, 0.86f), panel.anchorMax);
                AssertSliced(panel, skin.panel);
                Assert.IsNull(panel.GetComponent<Outline>(), "단색 테두리 없음");
                AssertSliced(Find(panel, "Header"), skin.headerBar);
                Assert.AreEqual("설정", Find(panel, "Header/Title").GetComponent<Text>().text);
                AssertSliced(Find(panel, "Footer"), skin.footerBox);
                StringAssert.Contains("버전", Find(panel, "Footer/Version").GetComponent<Text>().text);

                // 닫기·음소거: 그림만(글자 없음), 같은 크기, 오른쪽 위 세로, 제목 바와 겹치지 않음.
                var close = Find(panel, "Close");
                var sound = Find(panel, "SoundToggle");
                Assert.IsNull(close.GetComponentInChildren<Text>(), "X 글자 삭제");
                Assert.IsNull(sound.GetComponentInChildren<Text>(), "♪ 글자 삭제");
                Assert.AreEqual(skin.close, close.GetComponent<Image>().sprite);
                Assert.IsTrue(close.GetComponent<Image>().preserveAspect);
                Assert.AreEqual(close.rect.size.x, sound.rect.size.x, 0.5f);
                Assert.AreEqual(close.rect.size.y, sound.rect.size.y, 0.5f);
                Assert.Less(World(sound).yMax, World(close).yMin + 0.5f, "음소거는 닫기 아래");
                Assert.Less(World(Find(panel, "Header")).xMax, World(close).xMin, "제목 바와 닫기가 겹치지 않는다");

                // 음소거: 스피커 그림이 바뀌고, 다시 열어도 유지된다.
                audio.SetSoundOn(true);
                var soundImage = sound.GetComponent<Image>();
                Assert.AreEqual(skin.soundOn, soundImage.sprite);
                sound.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(skin.soundOff, soundImage.sprite);
                view.Close();
                yield return new WaitForSecondsRealtime(0.3f);
                view.Open();
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(skin.soundOff, soundImage.sprite, "다시 열어도 꺼짐");
                sound.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(skin.soundOn, soundImage.sprite);

                // 넓은 버튼: 색별 9-slice, 테두리 선 끔, 글자 색은 테두리 색, 눌림 0.8.
                var wide = new[] { ("Defaults", skin.wideCyan), ("Language", skin.wideGray), ("Credits", skin.wideYellow), ("Coupon", skin.wideGreen) };
                foreach (var (name, sprite) in wide)
                {
                    var button = Find(panel, name).GetComponent<Button>();
                    AssertSliced((RectTransform)button.transform, sprite);
                    var outline = button.GetComponent<Outline>();
                    Assert.IsTrue(outline == null || !outline.enabled, name + " 테두리 선 끔");
                    AssertPressed(button, name);
                    var label = button.GetComponentInChildren<Text>();
                    Assert.LessOrEqual(label.preferredWidth, label.rectTransform.rect.width + 0.5f, name + " 글자가 칸 안");
                }

                AssertPressed(close.GetComponent<Button>(), "Close");
                AssertPressed(sound.GetComponent<Button>(), "SoundToggle");

                // 음량 줄: 이름 바로 옆에 수치(초록), 따로 있던 수치 칸 없음. 0~100에서 글자가 칸을 넘지 않는다.
                var channels = new[] { VolumeChannel.Master, VolumeChannel.Bgm, VolumeChannel.Sfx };
                foreach (var channel in channels)
                {
                    var row = Find(panel, "Row_" + channel);
                    Assert.IsNull(Find(row, "LabelBox").GetComponent<Image>(), "이름+수치 칸은 배경 그림 없음");
                    Assert.IsNull(row.Find("Name"), "예전 이름 칸 없음");
                    Assert.IsNull(row.Find("Value"), "예전 오른쪽 수치 칸 없음");
                    var value = Find(row, "LabelBox/Value").GetComponent<Text>();
                    var slider = Find(row, "Slider").GetComponent<Slider>();
                    AssertSliced(Find(row, "Slider/Track"), skin.sliderTrack);
                    var fill = slider.fillRect.GetComponent<Image>();
                    Assert.AreEqual(skin.sliderFill, fill.sprite);
                    Assert.AreEqual(Image.Type.Sliced, fill.type);
                    Assert.AreEqual(skin.sliderKnob, slider.handleRect.GetComponent<Image>().sprite);
                    Assert.AreEqual(Image.Type.Simple, slider.handleRect.GetComponent<Image>().type);
                    AssertPressed(slider, channel + " 슬라이더");

                    foreach (var amount in new[] { 0, 50, 100 })
                    {
                        VolumeSettings.Set(channel, amount, false);
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        Assert.AreEqual(amount, slider.value, 0.01f);
                        StringAssert.StartsWith(new[] { "전체 음량", "배경음", "효과음" }[System.Array.IndexOf(channels, channel)] + "  <color=", value.text);
                        StringAssert.Contains(">" + amount + "<", value.text);
                        var generation = value.GetGenerationSettings(new Vector2(float.MaxValue, value.rectTransform.rect.height));
                        generation.resizeTextForBestFit = false;
                        generation.fontSize = Mathf.RoundToInt(20 * value.pixelsPerUnit);
                        var settingsWidth = value.cachedTextGenerator.GetPreferredWidth(value.text, generation) / value.pixelsPerUnit;
                        Assert.LessOrEqual(settingsWidth, value.rectTransform.rect.width + 0.5f, channel + " " + amount + " 글자가 칸 안(20px 기준)");

                        // 0이면 채움을 숨기고, 50·100은 트랙 홈 안에서 늘어난다. 손잡이는 트랙 높이의 1.6배.
                        Assert.AreEqual(amount > 0, fill.enabled, channel + " " + amount + " 채움");
                        var track = Find(row, "Slider/Track");
                        if (amount > 0)
                        {
                            Assert.LessOrEqual(World(slider.fillRect).yMax, World(track).yMax + 0.01f);
                            Assert.GreaterOrEqual(World(slider.fillRect).xMin, World(track).xMin - 0.01f);
                        }

                        Assert.AreEqual(track.rect.height * SettingsView.KnobTrackRatio, slider.handleRect.rect.height, 0.5f);
                        var knobCenter = World(slider.handleRect).center.x;
                        var expected = Mathf.Lerp(World(track).xMin, World(track).xMax, amount / 100f);
                        Assert.AreEqual(expected, knobCenter, 1f, channel + " " + amount + " 손잡이 위치");
                    }

                    Assert.GreaterOrEqual(((RectTransform)slider.transform).rect.height, 43f, "터치 영역은 예전 이상");
                }

                Debug.Log("설정 스킨 크기: 패널 " + panel.rect.size + ", 닫기 " + close.rect.size + ", 트랙 높이 " +
                          Find(panel, "Row_Master/Slider/Track").rect.height + ", 손잡이 " + Find(panel, "Row_Master/Slider").GetComponent<Slider>().handleRect.rect.size +
                          ", 슬라이더 칸 높이 " + Find(panel, "Row_Master/Slider").rect.height + ", 이름 칸 " + Find(panel, "Row_Master/LabelBox").rect.size);
            }
            finally
            {
                foreach (var pair in volumes)
                {
                    VolumeSettings.Set(pair.Key, pair.Value, false);
                }

                audio.SetSoundOn(soundBefore);
                Object.DestroyImmediate(canvas);
            }

            yield return new ExitPlayMode();
        }

        static void AssertSliced(RectTransform rect, Sprite sprite)
        {
            var image = rect.GetComponent<Image>();
            Assert.IsNotNull(image, rect.name);
            Assert.AreEqual(sprite, image.sprite, rect.name);
            Assert.AreEqual(Image.Type.Sliced, image.type, rect.name);
            Assert.IsTrue(image.fillCenter, rect.name);
            Assert.AreEqual(Color.white, image.color, rect.name);
        }

        static void AssertPressed(Selectable selectable, string name)
        {
            Assert.AreEqual(Selectable.Transition.ColorTint, selectable.transition, name);
            Assert.AreEqual(0.8f, selectable.colors.pressedColor.r, 0.01f, name + " 눌림 색");
            Assert.AreEqual(Color.white, selectable.colors.normalColor, name);
        }
    }
}
