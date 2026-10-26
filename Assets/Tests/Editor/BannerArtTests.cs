using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScrapFishing.Boat;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    public class BannerArtTests
    {
        const string Folder = "Assets/Resources/Gacha/Banners/";

        static List<Dictionary<string, object>> JsonBanners()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Gacha/oripa_banners.json");
            var root = (Dictionary<string, object>)MiniJson.Parse(asset.text);
            return ((List<object>)root["banners"]).Cast<Dictionary<string, object>>().ToList();
        }

        [Test]
        public void Images_ImportedAndMatchBannerIds()
        {
            var banners = JsonBanners();
            Assert.AreEqual(26, banners.Count);
            foreach (var banner in banners)
            {
                var id = (string)banner["id"];
                Assert.AreEqual("Gacha/Banners/" + id, banner["bannerImage"], id);
                var path = Folder + id + ".jpg";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, id);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, id);
                Assert.AreEqual(100f, importer.spritePixelsPerUnit, id);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode, id);
                Assert.AreEqual(1024, importer.maxTextureSize, id);
                Assert.IsFalse(importer.mipmapEnabled, id);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, id);
                Assert.AreEqual(TextureImporterCompression.Compressed, importer.textureCompression, id);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, id);
                Assert.AreEqual(new Vector2(1024f, 768f), sprite.rect.size, id);
            }

            Assert.AreEqual(File.ReadAllText("Assets/Resources/Gacha/oripa_banners.json"), File.ReadAllText("planning/oripa_banners.json"), "planning 사본도 같은 파일");
        }

        static readonly List<string> Loaded = new List<string>();

        [UnityTest]
        public IEnumerator Shop_ArtCardsLoadWhenVisible()
        {
            yield return new EnterPlayMode();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Boat.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();
            var before = Profiler.GetTotalAllocatedMemoryLong();
            typeof(BoatBootstrap).GetMethod("OpenMarket", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return new WaitForSecondsRealtime(0.6f);
            var view = Object.FindFirstObjectByType<GachaShopView>();
            var cards = view.CardStates().ToList();
            Assert.AreEqual(26, cards.Count, "전체 탭 26장");

            // 처음에는 화면에 보이는 카드만 그림을 불러오고 효과를 돌린다.
            var loadedAtOpen = cards.Count(c => c.Loaded);
            Assert.Greater(loadedAtOpen, 0);
            Assert.Less(loadedAtOpen, 26, "처음부터 전부 불러오지 않는다");
            foreach (var card in cards)
            {
                Assert.AreEqual(card.Loaded, card.Playing, card.Banner.Id + " 보이는 카드만 효과");
            }

            // 카드 모양: 높이 = 폭 × 0.6(아래 어두운 띠를 자름). 그림은 4:3 그대로 위에 맞춰 붙고, 아래만 RectMask2D로 잘린다.
            // 버튼은 예전(폭 × 0.75) 카드와 같은 크기(폭 0.23W, 높이 0.1275W)·같은 아래 여백(0.0225W)으로 오른쪽 아래.
            Canvas.ForceUpdateCanvases();
            var first = cards[0];
            var cardWidth = first.Root.rect.width;
            Assert.AreEqual(cardWidth * 0.6f, first.Root.rect.height, 1f, "카드 높이 = 폭 × 0.6");
            foreach (var card in cards)
            {
                Assert.IsNotNull(card.Root.GetComponent<RectMask2D>(), card.Banner.Id + " 아래를 자르는 마스크");
                var art = card.Art.rectTransform;
                Assert.AreEqual(cardWidth, art.rect.width, 1f);
                Assert.AreEqual(cardWidth * 0.75f, art.rect.height, 1f, card.Banner.Id + " 그림은 4:3 그대로");
                Assert.AreEqual(WorldTop(card.Root), WorldTop(art), 0.01f, "그림 위 끝 = 카드 위 끝");

                var text = (RectTransform)card.Root.Find("Text");
                Assert.LessOrEqual(text.anchorMax.x, 0.55f);
                var buttonsLeft = float.MaxValue;
                var buttonsTop = 0f;
                foreach (var button in card.Buttons)
                {
                    var rect = (RectTransform)button.transform;
                    Assert.AreEqual(cardWidth * 0.23f, rect.rect.width, 0.6f, card.Banner.Id + " 버튼 폭 그대로");
                    Assert.AreEqual(cardWidth * 0.1275f, rect.rect.height, 0.6f, card.Banner.Id + " 버튼 높이 그대로");
                    Assert.AreEqual(cardWidth * 0.0225f, rect.anchorMin.y * card.Root.rect.height, 0.6f, "아래 여백 그대로");
                    buttonsLeft = Mathf.Min(buttonsLeft, Local(card.Root, rect).xMin);
                    buttonsTop = Mathf.Max(buttonsTop, Local(card.Root, rect).yMax);
                }

                var last = (RectTransform)card.Buttons[card.Buttons.Length - 1].transform;
                Assert.AreEqual(0.97f, last.anchorMax.x, 1e-4f, card.Banner.Id + " 오른쪽 정렬");

                // 가격 줄·천장 게이지는 버튼 왼쪽 끝 8px 앞에서 끝나고, 태그·제목·설명은 버튼보다 위.
                var info = (RectTransform)card.Root.Find("Info");
                Assert.AreEqual(buttonsLeft - 8f, Local(card.Root, info).xMax, 0.6f, card.Banner.Id + " 가격 줄 오른쪽 끝");
                var gauge = card.Root.Find("GaugeTrack");
                if (gauge != null)
                {
                    Assert.AreEqual(buttonsLeft - 8f, Local(card.Root, (RectTransform)gauge).xMax, 0.6f, card.Banner.Id + " 게이지 오른쪽 끝");
                }

                Assert.Greater(Local(card.Root, text).yMin, buttonsTop, card.Banner.Id + " 제목·설명은 버튼보다 위");
                Assert.LessOrEqual(Local(card.Root, text).yMax, card.Root.rect.height, "카드 안");
                var title = text.Find("Name").GetComponent<Text>();
                // 제목: 자동 축소된 크기로 다시 그려 봤을 때 칸 높이 안에 다 들어가고, 18px 아래로 줄지 않는다.
                var generator = new TextGenerator();
                var size = title.rectTransform.rect.size;
                var settings = title.GetGenerationSettings(size);
                // 글자 생성기는 캔버스 배율(pixelsPerUnit)을 곱한 화면 픽셀로 계산하므로 UI 단위로 되돌려 비교한다.
                var ppu = title.pixelsPerUnit;
                generator.Populate(title.text, settings);
                var used = generator.fontSizeUsedForBestFit;
                Assert.GreaterOrEqual(used / ppu, 17.5f, card.Banner.Id + " 제목 최소 18px");
                settings.resizeTextForBestFit = false;
                settings.fontSize = used;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                generator.Populate(title.text, settings);
                Assert.LessOrEqual(generator.rectExtents.height / ppu, size.y + 1f,
                    card.Banner.Id + " 제목이 칸 안에 다 보인다(" + generator.lineCount + "줄, " + used / ppu + "px)");

                Assert.IsNull(card.Root.Find("Banner/Featured"), "픽업 장비 아이콘 숨김");
                Assert.IsNull(card.Root.Find("Banner/Showcase"), "쇼케이스 숨김");
                Assert.AreEqual(card.Banner.Type == GachaBanner_Type("Daily") ? 1 : 2, card.Buttons.Length);
            }

            // 대축제 설명은 2줄 안(넘치면 …), 전체 문구는 확인 팝업에서.
            Canvas.ForceUpdateCanvases();
            var festival = cards.First(c => c.Banner.Type == BannerType.Festival);
            var desc = festival.Desc.GetComponent<Text>();
            desc.cachedTextGenerator.Populate(desc.text, desc.GetGenerationSettings(desc.rectTransform.rect.size));
            Assert.LessOrEqual(desc.cachedTextGenerator.lineCount, 2, "대축제 설명 2줄");
            Debug.Log("대축제 카드 설명: " + desc.text + " (잘림 " + festival.Desc.Truncated + ")");

            // 비활성 표시: 지금 로직 그대로(데일리 완료·축제 소진·미제공은 회색).
            foreach (var card in cards)
            {
                for (var i = 0; i < card.Buttons.Length; i++)
                {
                    var block = view.Shop.Block(card.Banner, card.Banner.PullCounts[i]);
                    var locked = block == BannerBlock.DailyDone || block == BannerBlock.LimitReached || block == BannerBlock.NotOffered;
                    Assert.AreEqual(!locked, card.Buttons[i].interactable, card.Banner.Id + " " + block);
                }
            }

            // 스크롤하면서 보이는 카드가 차례로 불러와진다. 끝까지 내리면 26장 모두.
            for (var p = 1f; p >= -0.01f; p -= 0.05f)
            {
                view.Scroll.verticalNormalizedPosition = Mathf.Max(0f, p);
                yield return new WaitForSecondsRealtime(0.15f);
            }

            cards = view.CardStates().ToList();
            Loaded.Clear();
            long textureBytes = 0;
            foreach (var card in cards)
            {
                Assert.IsTrue(card.Loaded, card.Banner.Id + " 스크롤해서 보이면 불러온다");
                Assert.IsNotNull(card.Art.sprite, card.Banner.Id);
                Assert.AreEqual(card.Banner.Id, card.Art.sprite.name, "파일 이름 = 배너 id");
                textureBytes += Profiler.GetRuntimeMemorySizeLong(card.Art.sprite.texture);
                Loaded.Add(card.Banner.Id);
            }

            Assert.IsFalse(cards[0].Playing, "맨 위 카드는 화면 밖이라 멈춤");
            var viewportHeight = view.Scroll.viewport.rect.height;
            var spacing = view.Scroll.content.GetComponent<VerticalLayoutGroup>().spacing;
            Debug.Log($"한 화면 카드 수: 지금 {viewportHeight / (cardWidth * 0.6f + spacing):0.00}장, 예전(폭×0.75) {viewportHeight / (cardWidth * 0.75f + spacing):0.00}장 (목록 높이 {viewportHeight:0}px, 카드 {cardWidth * 0.6f:0}px ← {cardWidth * 0.75f:0}px)");
            Debug.Log($"배너 그림 메모리: 처음 화면 {loadedAtOpen}장, 26장 모두 {textureBytes / 1024f / 1024f:0.0}MB (장당 {textureBytes / 26f / 1024f:0}KB), 오리파 열기 전후 할당 증가 {(Profiler.GetTotalAllocatedMemoryLong() - before) / 1024f / 1024f:0.0}MB");

            // 머신 탭: 상시 1 + 픽업 3 = 4장씩, 각자 그 머신 그림.
            var select = typeof(GachaShopView).GetMethod("SelectTab", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var slot in new[] { GearSlot.Lure, GearSlot.Rod, GearSlot.Boat, GearSlot.Drone, GearSlot.Fisher, GearSlot.Helmet })
            {
                select.Invoke(view, new object[] { (GearSlot?)slot });
                yield return new WaitForSecondsRealtime(0.2f);
                var active = view.CardStates().Where(c => c.Root.gameObject.activeSelf).ToList();
                Assert.AreEqual(4, active.Count, slot + " 탭 4장");
                foreach (var card in active)
                {
                    Assert.AreEqual(slot, card.Banner.Machine.Slot);
                    StringAssert.StartsWith("banner_" + slot.ToString().ToLowerInvariant() + "_", card.Banner.Id);
                }
            }

            select.Invoke(view, new object[] { null });
            yield return null;

            // 확인 팝업: 같은 그림을 위에, 카드에서 잘린 설명 전체.
            var popup = (GachaConfirmPopup)typeof(GachaShopView).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            typeof(GachaShopView).GetMethod("ShowConfirm", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { festival.Banner, 1 });
            yield return null;
            Assert.IsTrue(popup.IsOpen);
            Assert.AreEqual(festival.Banner.BannerSprite, popup.Art.sprite, "팝업에 같은 배너 그림");
            Assert.IsTrue(popup.Art.gameObject.activeSelf);
            Assert.AreEqual(festival.Banner.DescKo, popup.Description.text, "잘린 설명 전체");
            popup.Hide();
            yield return new ExitPlayMode();
        }

        static float WorldTop(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1].y;
        }

        // 카드 안 좌표(왼쪽 아래 원점, 카드 픽셀).
        static Rect Local(RectTransform card, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var a = card.InverseTransformPoint(corners[0]) - (Vector3)card.rect.min;
            var b = card.InverseTransformPoint(corners[2]) - (Vector3)card.rect.min;
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        static BannerType GachaBanner_Type(string name)
        {
            return (BannerType)System.Enum.Parse(typeof(BannerType), name);
        }
    }
}
