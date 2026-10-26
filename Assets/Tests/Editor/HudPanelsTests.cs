using System.Collections;
using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    public class HudPanelsTests
    {
        static readonly string[] Files =
        {
            "depth_panel", "zone_tag", "scrap_panel", "scrap_panel_full", "scrap_bar_fill", "time_panel", "time_panel_warning", "dive_full", "dive_empty"
        };

        [Test]
        public void Sprites_ImportedAndSkinBaked()
        {
            foreach (var name in Files)
            {
                var path = HudUiImportPostprocessor.Root + name + ".png";
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
                Assert.IsTrue(importer.GetPlatformTextureSettings("WebGL").overridden, name + " WebGL");
                Assert.AreEqual(name == "scrap_bar_fill" ? Vector4.one : Vector4.zero, importer.spriteBorder, name + " 테두리");
            }

            var skin = AssetDatabase.LoadAssetAtPath<HudSkin>("Assets/Resources/UI/HudSkin.asset");
            Assert.IsNotNull(skin);
            Assert.IsTrue(skin.Complete);
            Assert.AreEqual(1f, skin.hudScale);
            Assert.AreEqual(0.305f, skin.depthNumber.xMin, 1e-4f, "hud_layout.json에서 옮긴 글자 영역");
            Assert.AreEqual(0.236f, skin.scrapBar.yMax, 1e-4f);
            Assert.AreEqual(0.931f, skin.zoneText.xMax, 1e-4f);
        }

        static GameObject MakeArea(out RectTransform area)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas.transform, false);
            area.sizeDelta = new Vector2(480f, 854f);
            return canvas;
        }

        // 480×854 기준 위치(왼쪽 아래 원점).
        static Rect InArea(RectTransform area, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var a = area.InverseTransformPoint(corners[0]) - (Vector3)area.rect.min;
            var b = area.InverseTransformPoint(corners[2]) - (Vector3)area.rect.min;
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        [UnityTest]
        public IEnumerator Panels_SizeStateAndLayout()
        {
            yield return new EnterPlayMode();
            var canvas = MakeArea(out var area);
            var skin = HudSkin.Load();
            var panels = new HudPanels(area, skin);
            try
            {
                // TIME: 숫자만, 10초 경계에서 빨간 패널.
                panels.SetTime(11);
                Assert.AreEqual("11", panels.TimeText.text);
                Assert.IsFalse(panels.Warning);
                Assert.AreEqual(skin.timePanel, panels.TimeImage.sprite);
                panels.SetTime(10);
                Assert.IsTrue(panels.Warning, "10초 이하");
                Assert.AreEqual(skin.timePanelWarning, panels.TimeImage.sprite);
                Assert.Greater(panels.TimeText.color.r, 0.9f);
                Assert.Less(panels.TimeText.color.g, 0.5f, "숫자도 빨강");
                panels.SetTime(0);
                Assert.AreEqual("0", panels.TimeText.text);
                panels.SetTime(-1);
                Assert.AreEqual(string.Empty, panels.TimeText.text);
                Assert.IsFalse(panels.Warning);

                // 수심: 숫자만(m는 그림), 구역 태그.
                panels.SetDepth(0f);
                Assert.AreEqual("0", panels.DepthText.text);
                Assert.AreEqual(DepthZone.Name(0f), panels.ZoneText.text);
                Assert.AreEqual(TextAnchor.MiddleRight, panels.DepthText.alignment);
                panels.SetDepth(0.93f);
                Assert.AreEqual(Mathf.RoundToInt(DepthZone.Meters(0.93f)).ToString(), panels.DepthText.text);
                Assert.AreEqual(DepthZone.Name(0.93f), panels.ZoneText.text);

                // 보트 수심 0~1000m, 구역 경계 250·500·750m.
                panels.SetDepth(1f);
                Assert.AreEqual("1000", panels.DepthText.text);
                Assert.AreEqual("심연", panels.ZoneText.text);
                panels.SetDepth(0.25f);
                Assert.AreEqual("250", panels.DepthText.text);
                Assert.AreEqual("드론 묘지", panels.ZoneText.text);
                panels.SetDepth(0.2499f);
                Assert.AreEqual("표층", panels.ZoneText.text);
                panels.SetDepth(0.5f);
                Assert.AreEqual("500", panels.DepthText.text);
                Assert.AreEqual("슬러지", panels.ZoneText.text);
                panels.SetDepth(0.75f);
                Assert.AreEqual("750", panels.DepthText.text);
                Assert.AreEqual("심연", panels.ZoneText.text);

                // 잠수: 해저 평원, 수면 1500m ~ 바닥 1600m(보트 최대 1000m보다 깊다).
                panels.SetDiveDepth(0f);
                Assert.AreEqual("1500", panels.DepthText.text);
                Assert.AreEqual("해저 평원", panels.ZoneText.text);
                panels.SetDiveDepth(0.5f);
                Assert.AreEqual("1550", panels.DepthText.text);
                panels.SetDiveDepth(1f);
                Assert.AreEqual("1600", panels.DepthText.text);
                Canvas.ForceUpdateCanvases();
                Assert.LessOrEqual(panels.DepthText.preferredWidth, panels.DepthText.rectTransform.rect.width + 0.5f, "4자리 수심이 칸 안");
                Assert.LessOrEqual(panels.ZoneText.preferredWidth, panels.ZoneText.rectTransform.rect.width + 0.5f, "해저 평원이 태그 안");

                // 스크랩: 0이면 바 숨김, 중간은 비율, 가득 차면 초록 패널.
                panels.SetScrap(0, 10);
                Assert.AreEqual("0/10", panels.ScrapText.text);
                Assert.IsFalse(panels.ScrapFill.enabled);
                Assert.AreEqual(skin.scrapPanel, panels.ScrapImage.sprite);
                panels.SetScrap(4, 10);
                Assert.IsTrue(panels.ScrapFill.enabled);
                Assert.AreEqual(0.4f, panels.ScrapFill.rectTransform.anchorMax.x, 1e-4f);
                Assert.AreEqual(Image.Type.Sliced, panels.ScrapFill.type);
                Assert.IsFalse(panels.Full);
                panels.SetScrap(10, 10);
                Assert.IsTrue(panels.Full);
                Assert.AreEqual(1f, panels.ScrapFill.rectTransform.anchorMax.x, 1e-4f);
                Assert.AreEqual(skin.scrapPanelFull, panels.ScrapImage.sprite);
                Assert.AreEqual(Palette.NeonGreen, panels.ScrapText.color);
                Assert.IsFalse(panels.ScrapText.text.Contains("FULL"));

                // 잠수 칸: CastsPerDive만큼, DiveProgress만큼 채움. 3·4칸 모두.
                panels.SetDive(1, 3, false);
                Assert.AreEqual(3, panels.DiveCells.Count);
                Assert.AreEqual(skin.diveFull, panels.DiveCells[0].sprite);
                Assert.AreEqual(skin.diveEmpty, panels.DiveCells[1].sprite);
                Assert.AreEqual(skin.diveEmpty, panels.DiveCells[2].sprite);
                Assert.IsFalse(panels.DiveBlinking);
                panels.SetDive(2, 4, false);
                yield return null;
                Assert.AreEqual(4, panels.DiveCells.Count);
                Assert.AreEqual(4, area.Find("DiveCells").childCount, "예전 칸은 지운다");
                Assert.AreEqual(skin.diveFull, panels.DiveCells[1].sprite);
                Assert.AreEqual(skin.diveEmpty, panels.DiveCells[2].sprite);
                panels.SetDive(4, 4, true);
                Assert.IsTrue(panels.DiveBlinking, "잠수 가능이면 깜빡인다");
                yield return new WaitForSecondsRealtime(0.45f);
                Assert.Less(panels.DiveCells[0].color.a, 0.9f, "채운 칸이 흐려졌다 돌아온다");
                panels.SetDive(0, 4, false);
                Assert.IsFalse(panels.DiveBlinking);
                Assert.AreEqual(1f, panels.DiveCells[0].color.a);

                // 크기(480×854, hudScale 1)와 위치.
                Canvas.ForceUpdateCanvases();
                var depth = InArea(area, panels.Depth);
                var zone = InArea(area, panels.Zone);
                var scrap = InArea(area, panels.Scrap);
                var time = InArea(area, panels.TimeRect);
                Assert.AreEqual(124f, depth.width, 0.5f);
                Assert.AreEqual(43f, depth.height, 1f);
                Assert.AreEqual(88f, zone.width, 0.5f);
                Assert.AreEqual(20f, zone.height, 1f);
                Assert.AreEqual(112f, scrap.width, 0.5f);
                Assert.AreEqual(55f, scrap.height, 1f);
                Assert.AreEqual(86f, time.width, 0.5f);
                Assert.AreEqual(30f, time.height, 1f);
                Assert.AreEqual(1f, zone.yMax - depth.yMin, 0.01f, "태그 위 끝이 패널 아래에 1px 겹친다");
                Assert.AreEqual(depth.xMin + 14f, zone.xMin, 0.5f, "태그는 패널 왼쪽에서 14px 안쪽");
                Assert.AreEqual(764f, scrap.yMax, 0.01f, "스크랩은 칩 패널(아래 끝 770) 아래 6px");
                Assert.AreEqual(10f, scrap.xMin, 0.01f);
                Assert.AreEqual(145f, depth.yMax, 0.01f, "수심 패널은 수심 게이지 틀(아래 끝 약 149) 아래");
                Assert.AreEqual(475f, depth.xMax, 0.5f, "오른쪽 맞춤");
                Assert.Greater(zone.yMin, 77f, "아래 가운데 힌트(위 끝 77)보다 위");
                Assert.LessOrEqual(time.yMax, 816f, "톱니 버튼(아래 끝 816) 아래");
                Assert.AreEqual(475f, time.xMax, 0.5f);

                // 수심 게이지: 막대 x 434~454, y 161~502, 틀까지 약 x 421~467, y 149~516.
                var gauge = Rect.MinMaxRect(421f, 149f, 467f, 516f);
                var cells = new Rect[panels.DiveCells.Count];
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = InArea(area, panels.DiveCells[i].rectTransform);
                    Assert.AreEqual(34f, cells[i].width, 0.5f);
                    Assert.AreEqual(33f, cells[i].height, 0.5f);
                    Assert.AreEqual(475f, cells[i].xMax, 0.5f, "오른쪽 끝");
                    Assert.IsFalse(cells[i].Overlaps(gauge), "수심 게이지와 안 겹침");
                }

                Assert.AreEqual(3f, time.yMin - cells[0].yMax, 0.01f, "TIME 바로 아래");
                Assert.AreEqual(3f, cells[0].yMin - cells[1].yMax, 0.01f, "칸 사이 3px");
                var boat = Rect.MinMaxRect(17f, 410f, 222f, 590f);
                foreach (var rect in new[] { depth, zone, scrap, time })
                {
                    Assert.IsFalse(rect.Overlaps(gauge), "수심 게이지와 안 겹침");
                    Assert.IsFalse(rect.Overlaps(boat), "보트·낚시꾼과 안 겹침");
                }

                Debug.Log($"HUD 위치(480×854, 왼쪽 아래 원점): 수심 {depth} 태그 {zone} 스크랩 {scrap} TIME {time} 잠수칸1 {cells[0]} 잠수칸4 {cells[3]}");
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }

            yield return new ExitPlayMode();
        }
    }
}
