using System.Collections;
using System.Linq;
using NUnit.Framework;
using ScrapFishing.Gacha;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    // 보관함·결과창 UI를 메모리 저장소 상점으로 띄워 확인한다(실제 저장 데이터는 건드리지 않는다).
    public class InventoryUiTests
    {
        // EnterPlayMode는 도메인을 다시 불러와 코루틴의 람다 캡처 객체가 사라지므로, 콜백 결과는 정적 필드에 둔다.
        static int _refunded;

        static void AddRefunded(int dollars)
        {
            _refunded += dollars;
        }

        static void IgnoreRefund(System.Collections.Generic.List<RefundPick> picks, RectTransform from)
        {
        }

        static void IgnoreStore(System.Collections.Generic.List<RefundPick> picks)
        {
        }

        static void IgnoreClose()
        {
        }

        static GachaShop MakeShop(GachaCatalog catalog, MemoryWallet wallet)
        {
            var shop = new GachaShop(catalog, new GachaService(new SystemGachaRandom(7)), wallet, new MemoryKeyValueStore(), () => 0L);
            shop.EnsureStarterGear();
            shop.Inventory.AddPulled(new[] { "lure_rare_01", "lure_rare_01", "rod_epic_01", "boat_normal_02" }.Select(catalog.FindGear).ToList(), "test", 0L);
            shop.Store("lure_rare_01", 1);
            return shop;
        }

        static GameObject MakeArea(out RectTransform area)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            area = new GameObject("GameArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas.transform, false);
            area.sizeDelta = new Vector2(480f, 854f);
            return canvas;
        }

        [UnityTest]
        public IEnumerator Inventory_SlotsEffectsTabAndBulkRefund()
        {
            yield return new EnterPlayMode();
            var machines = OripaCatalogJson.Load();
            Assert.IsNotNull(machines, "catalog");
            var catalog = new GachaCatalog(machines);
            var wallet = new MemoryWallet(0);
            var shop = MakeShop(catalog, wallet);
            var canvas = MakeArea(out var area);
            _refunded = 0;
            var view = new GachaInventoryView(area, AddRefunded);
            view.Show(shop);
            yield return null;

            var root = area.Find("GachaInventory");
            // 장착 칸: 이름 없이 아이콘만 크게.
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var cell = root.Find("Slots/Slot_" + slot);
                Assert.IsNotNull(cell, slot.ToString());
                Assert.IsNull(cell.Find("Name"), slot + " 이름 제거");
                var icon = (RectTransform)cell.Find("Icon");
                Assert.IsNotNull(icon, slot + " 아이콘");
                Assert.Greater(icon.anchorMax.y - icon.anchorMin.y, 0.7f, slot + " 아이콘을 크게");
            }

            // 총 효과: 탭 버튼으로 열고 닫는다.
            var panel = root.Find("EffectsPanel");
            Assert.IsFalse(panel.gameObject.activeSelf);
            var tab = root.Find("EffectsTab").GetComponent<Button>();
            tab.onClick.Invoke();
            Assert.IsTrue(panel.gameObject.activeSelf);
            StringAssert.Contains("흡착 반경 +2%", panel.Find("Summary").GetComponent<Text>().text, "lure_normal_01 효과가 총 효과에 보인다");
            tab.onClick.Invoke();
            Assert.IsFalse(panel.gameObject.activeSelf);

            // 일괄 정산: rod_epic_01만 체크 해제하고 나머지 보관·미처리 장비를 정산한다. 장착 중인 노멀은 그대로.
            var rodCheck = root.Find("List/Viewport/Content/Gear_rod_epic_01/Check").GetComponent<Button>();
            rodCheck.onClick.Invoke();
            yield return null;
            var expected = shop.RefundValue("lure_rare_01", 2) + shop.RefundValue("boat_normal_02", 1);
            var bulk = root.Find("BulkRefund").GetComponent<Button>();
            bulk.onClick.Invoke();
            Assert.AreEqual(0, wallet.Balance, "첫 번째 누름은 확인만");
            bulk.onClick.Invoke();
            yield return null;
            Assert.AreEqual(expected, wallet.Balance);
            Assert.AreEqual(expected, _refunded);
            Assert.AreEqual(0, shop.Inventory.Count("lure_rare_01", GearState.Stored) + shop.Inventory.Count("lure_rare_01", GearState.Unprocessed));
            Assert.AreEqual(0, shop.Inventory.Count("boat_normal_02", GearState.Unprocessed));
            Assert.AreEqual(1, shop.Inventory.Count("rod_epic_01", GearState.Unprocessed), "체크 해제한 장비는 남는다");
            Assert.AreEqual("lure_normal_01", shop.Inventory.EquippedId(GearSlot.Lure), "장착 중인 장비는 정산하지 않는다");

            Object.DestroyImmediate(canvas);
            foreach (var machine in machines)
            {
                foreach (var gear in machine.Gears)
                {
                    Object.DestroyImmediate(gear);
                }

                Object.DestroyImmediate(machine);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Result_HasNoAgainButton()
        {
            yield return new EnterPlayMode();
            var machines = OripaCatalogJson.Load();
            var catalog = new GachaCatalog(machines);
            var shop = new GachaShop(catalog, new GachaService(new SystemGachaRandom(11)), new MemoryWallet(10000000), new MemoryKeyValueStore(), () => 0L);
            var banner = catalog.BannerSet.Find("banner_rod_standard");
            Assert.IsTrue(shop.TryPull(banner, 10, out var result));

            var canvas = MakeArea(out var area);
            var view = new GachaResultView(area, IgnoreRefund, IgnoreStore, IgnoreClose);
            view.Show(shop, result);
            yield return null;

            var again = area.Find("GachaResult/Again");
            Assert.IsTrue(again == null || !again.gameObject.activeSelf, "연속 뽑기 버튼 없음");
            var title = (RectTransform)area.Find("GachaResult/Title");
            var close = (RectTransform)area.Find("GachaResult/Close");
            Assert.LessOrEqual(title.anchorMax.x, close.anchorMin.x, "제목은 닫기 버튼 앞까지");

            Object.DestroyImmediate(canvas);
            foreach (var m in machines)
            {
                foreach (var gear in m.Gears)
                {
                    Object.DestroyImmediate(gear);
                }

                Object.DestroyImmediate(m);
            }

            yield return new ExitPlayMode();
        }
    }
}
