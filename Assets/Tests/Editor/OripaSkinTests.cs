using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ScrapFishing.Data;
using ScrapFishing.Gacha;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    // 오리파·보관함 스킨: 가져오기 설정, 화면별 그림, 글자 겹침, 스킨이 없을 때 예전 모양.
    public class OripaSkinTests
    {
        [Test]
        public void Sprites_ImportedWithManifestBorders()
        {
            var files = Directory.GetFiles(OripaUiImportPostprocessor.Root, "*.png");
            Assert.AreEqual(74, files.Length);
            foreach (var file in files)
            {
                var path = file.Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, name);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, name);
                Assert.AreEqual(100f, importer.spritePixelsPerUnit, name);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode, name);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, name);
                Assert.IsTrue(importer.GetPlatformTextureSettings("WebGL").overridden, name);
                Assert.IsFalse(importer.mipmapEnabled, name);
                Assert.IsTrue(importer.alphaIsTransparency, name);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, name);
                Assert.AreEqual(1024, importer.maxTextureSize, name);
                var ltrb = OripaUiImportPostprocessor.BorderOf(name);
                var expected = ltrb != null ? new Vector4(ltrb[0], ltrb[3], ltrb[2], ltrb[1]) : Vector4.zero;
                Assert.AreEqual(expected, importer.spriteBorder, name + " 테두리(L,B,R,T)");
            }

            // [L,T,R,B] = [70,110,70,70] → (L,B,R,T) = (70,70,70,110).
            Assert.AreEqual(new Vector4(70, 70, 70, 110), ((TextureImporter)AssetImporter.GetAtPath(OripaUiImportPostprocessor.Root + "popup_gold.png")).spriteBorder);
            var skin = AssetDatabase.LoadAssetAtPath<OripaSkin>("Assets/Resources/UI/OripaSkin.asset");
            Assert.IsNotNull(skin);
            Assert.AreEqual(74, skin.sprites.Count);
            Assert.IsNotNull(skin.Get("row_synchro"));
        }

        static GameObject MakeArea(out RectTransform area)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas.transform, false);
            area.sizeDelta = new Vector2(480f, 854f);
            return canvas;
        }

        static Rect World(Transform target)
        {
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // 글자가 칸 안에 다 들어가는지(글자 생성기 크기는 화면 픽셀이라 UI 단위로 되돌린다).
        static void AssertFits(Text text, string what)
        {
            Canvas.ForceUpdateCanvases();
            var size = text.rectTransform.rect.size;
            var settings = text.GetGenerationSettings(new Vector2(size.x, 0f));
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            var generator = new TextGenerator();
            generator.Populate(text.text, settings);
            var width = text.horizontalOverflow == HorizontalWrapMode.Overflow
                ? generator.GetPreferredWidth(text.text, text.GetGenerationSettings(new Vector2(float.MaxValue, size.y))) / text.pixelsPerUnit
                : 0f;
            Assert.LessOrEqual(width, size.x + 1f, what + " 폭");
            Assert.LessOrEqual(generator.rectExtents.height / text.pixelsPerUnit, size.y + 2f, what + " 높이");
        }

        // EnterPlayMode가 도메인을 다시 불러오면 람다가 잡은 지역 변수가 사라지므로, 이름 찾기는 람다 없이 한다.
        static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        static GachaShop MakeShop(GachaCatalog catalog, out GearData synchro)
        {
            var shop = new GachaShop(catalog, new GachaService(new SystemGachaRandom(5)), new MemoryWallet(10000000), new MemoryKeyValueStore(), () => 0L);
            shop.EnsureStarterGear();
            var gears = new List<GearData>();
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                gears.AddRange(catalog.AllGears.Where(g => g.Rarity == rarity).Take(2));
            }

            shop.Inventory.AddPulled(gears, "test", 0L);
            synchro = gears.First(g => g.Rarity == ItemRarity.Synchro);
            shop.Store(synchro.Id, 1);
            shop.Equip(synchro.Id);
            shop.Inventory.AddPulled(new List<GearData> { synchro, gears.First(g => g.Rarity == ItemRarity.Legend) }, "test", 0L);
            return shop;
        }

        [UnityTest]
        public IEnumerator Screens_UseSkinWithoutOverlaps()
        {
            yield return new EnterPlayMode();
            OripaSkin.Disabled = false;
            var catalog = GachaCatalog.LoadDefault();
            var shop = MakeShop(catalog, out var synchro);

            var canvas = MakeArea(out var area);
            try
            {
                // 확인 팝업: popup_cyan, 그림 왼쪽 아래 태그, 안내 줄 + 등급 칩 5개, 뽑기 버튼 글자는 버튼 안.
                var festival = catalog.BannerSet.Find("banner_festival");
                var popup = new GachaConfirmPopup(area, (b, c) => { });
                popup.Show(shop, festival, 10);
                yield return null;
                var panel = area.Find("GachaConfirm/Panel");
                Assert.AreEqual("popup_cyan", panel.GetComponent<Image>().sprite.name);
                Assert.IsTrue(popup.Tag.gameObject.activeSelf);
                var banner = World(panel.Find("Banner"));
                var tag = World(popup.Tag.transform);
                Assert.Less(tag.xMax, banner.center.x + 20f, "태그는 그림 왼쪽");
                Assert.Less(tag.yMin, banner.yMin, "태그가 그림 아래 모서리에 걸친다");
                Assert.Greater(tag.yMax, banner.yMin);
                Assert.Less(World(panel.Find("Desc")).yMax, tag.yMin + 1f, "설명은 태그 아래");
                Assert.AreEqual(5, popup.Rows.Find("Rates").GetComponentsInChildren<Image>().Count(i => i.sprite != null && i.sprite.name.StartsWith("chip_")));
                var confirmText = GachaUi.LabelOf(popup.ConfirmButton);
                StringAssert.Contains("3% 할인", confirmText.text);
                AssertFits(confirmText, "뽑기 버튼 글자");
                Assert.AreEqual("btn_green", ((Image)popup.ConfirmButton.targetGraphic).sprite.name);
                popup.Hide();

                // 결과 목록: 제목은 닫기와 겹치지 않고, 줄·체크 칸·아이콘 칸은 등급 그림.
                var ten = new GachaPullResult { Machine = catalog.Machines[0], Count = 10, Banner = festival };
                foreach (var gear in catalog.AllGears.Where(g => g.Rarity >= ItemRarity.Epic).Take(10))
                {
                    ten.Items.Add(new PulledGear(gear, false));
                }

                var result = new GachaResultView(area, (p, r) => { }, p => { }, () => { });
                result.Show(shop, ten);
                yield return new WaitForSecondsRealtime(0.3f);
                var root = area.Find("GachaResult");
                var title = root.Find("Title").GetComponent<Text>();
                AssertFits(title, "결과 제목");
                Assert.Less(World(title.transform).xMax, World(root.Find("Close")).xMin, "제목과 닫기가 겹치지 않는다");
                var card = FindDeep(root, "Card_" + ten.Items[0].Gear.Id);
                Assert.AreEqual(OripaLook.Of("row", ten.Items[0].Gear.Rarity), card.GetComponent<Image>().sprite.name);
                Assert.AreEqual(OripaLook.Of("icon_slot", ten.Items[0].Gear.Rarity), card.Find("Icon").GetComponent<Image>().sprite.name);
                StringAssert.StartsWith("checkbox_", card.Find("Box").GetComponent<Image>().sprite.name);
                Assert.AreEqual("pill", root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bar").GetComponent<Image>().sprite.name);
                result.Hide();

                // 보관함: 싱크로 장착 칸·줄은 크리스탈 그림, 체크 칸은 아이콘 왼쪽 바깥, 등급 칩·개수는 이름 아래 줄.
                var inventory = new GachaInventoryView(area, d => { });
                inventory.Show(shop);
                yield return new WaitForSecondsRealtime(0.3f);
                var inv = area.Find("GachaInventory");
                var slot = inv.Find("Slots/Slot_" + synchro.Slot);
                Assert.AreEqual("equip_slot_synchro", slot.GetComponent<Image>().sprite.name, "싱크로 장착 칸");
                var row = FindDeep(inv, "Gear_" + synchro.Id);
                Assert.AreEqual("row_synchro", row.GetComponent<Image>().sprite.name);
                Assert.AreEqual("icon_slot_synchro", row.Find("Icon").GetComponent<Image>().sprite.name);
                Assert.AreEqual("badge_equipped", row.Find("EquippedBadge").GetComponent<Image>().sprite.name);
                Assert.IsNull(row.Find("Unequip"), "장착 칸은 비울 수 없어 해제 버튼이 없다(교체만)");
                var check = row.Find("Check");
                Assert.IsNotNull(check, "보관 중인 장비는 체크 칸");
                Assert.LessOrEqual(World(check).xMax, World(row.Find("Icon")).xMin, "체크 칸이 아이콘을 가리지 않는다");
                var name = World(row.Find("Name"));
                Assert.LessOrEqual(World(row.Find("RarityPill")).yMax, name.yMin + 0.5f, "등급 칩은 이름 아래 줄");
                Assert.LessOrEqual(World(row.Find("Count")).yMax, name.yMin + 0.5f, "개수는 이름 아래 줄");
                Assert.LessOrEqual(World(row.Find("RarityPill")).xMax, World(row.Find("Count")).xMin + 0.5f, "등급 칩과 개수가 겹치지 않는다");
                Assert.AreEqual("btn_refund", row.Find("Refund").GetComponent<Image>().sprite.name);
                inv.Find("EffectsTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.AreEqual("popup_cyan", inv.Find("EffectsPanel").GetComponent<Image>().sprite.name);
                Assert.IsNotNull(inv.Find("EffectsPanel/Table/Name1"), "총 효과는 한 줄에 하나씩");
                inventory.Hide();
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }

            yield return new ExitPlayMode();
        }

        // 스킨이 없으면 예전 단색 모양(선 테두리)으로 그대로 동작한다.
        [UnityTest]
        public IEnumerator WithoutSkin_FallsBackToSolidLook()
        {
            yield return new EnterPlayMode();
            OripaSkin.Disabled = true;
            var catalog = GachaCatalog.LoadDefault();
            var shop = MakeShop(catalog, out var synchro);
            var canvas = MakeArea(out var area);
            try
            {
                Assert.IsFalse(OripaLook.On);
                var popup = new GachaConfirmPopup(area, (b, c) => { });
                popup.Show(shop, catalog.BannerSet.Find("banner_festival"), 10);
                yield return null;
                var panel = area.Find("GachaConfirm/Panel");
                Assert.IsNull(panel.GetComponent<Image>().sprite);
                Assert.IsTrue(panel.GetComponent<Outline>().enabled, "선 테두리 그대로");
                Assert.IsTrue(area.Find("GachaConfirm/Panel/Info").gameObject.activeSelf, "안내는 예전 글");

                var inventory = new GachaInventoryView(area, d => { });
                inventory.Show(shop);
                yield return null;
                var row = FindDeep(area.Find("GachaInventory"), "Gear_" + synchro.Id);
                Assert.IsNull(row.GetComponent<Image>().sprite);
                Assert.IsNotNull(row.Find("Check/Mark"), "예전 체크 표시");
            }
            finally
            {
                OripaSkin.Disabled = false;
                Object.DestroyImmediate(canvas);
            }

            yield return new ExitPlayMode();
        }
    }
}
