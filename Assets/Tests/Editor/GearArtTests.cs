using System.Linq;
using NUnit.Framework;
using ScrapFishing.Boat;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class GearArtTests
    {
        GachaMachineData[] _machines;
        GachaCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _machines = OripaCatalogJson.Load();
            Assert.IsNotNull(_machines);
            _catalog = new GachaCatalog(_machines);
            GearAnchors.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var machine in _machines)
            {
                foreach (var gear in machine.Gears)
                {
                    Object.DestroyImmediate(gear);
                }

                Object.DestroyImmediate(machine);
            }
        }

        GearData[] Gears(GearSlot slot)
        {
            return _catalog.MachineForSlot(slot).Gears.ToArray();
        }

        [Test]
        public void Icons_AllNinetyLoadAsFullSizeSprites()
        {
            var gears = _machines.SelectMany(m => m.Gears).ToArray();
            Assert.AreEqual(90, gears.Length);
            foreach (var gear in gears)
            {
                Assert.IsNotNull(gear.Icon, gear.Id);
                Assert.AreEqual(128f, gear.Icon.rect.width, gear.Id + " Single로 가져와 잘리지 않아야 한다");
                Assert.AreEqual(100f, gear.Icon.pixelsPerUnit, gear.Id);
            }
        }

        [Test]
        public void Layers_BoatAndFisherHaveFifteenEach()
        {
            foreach (var slot in new[] { GearSlot.Boat, GearSlot.Fisher })
            {
                var gears = Gears(slot);
                Assert.AreEqual(15, gears.Length, slot.ToString());
                foreach (var gear in gears)
                {
                    Assert.IsNotNull(gear.Layer, gear.Id);
                    Assert.AreEqual(256f, gear.Layer.rect.width, gear.Id);
                }
            }

            Assert.IsNull(_catalog.FindGear("lure_normal_01").Layer, "레이어는 보트·피셔만");
        }

        [Test]
        public void Anchors_ParseAndCoverEveryId()
        {
            Assert.AreEqual(15, GearAnchors.Rods.Count);
            Assert.AreEqual(15, GearAnchors.Boats.Count);
            Assert.AreEqual(15, GearAnchors.Fishers.Count);
            foreach (var gear in Gears(GearSlot.Rod))
            {
                Assert.IsTrue(GearAnchors.Rods.TryGetValue(gear.Id, out var rod), gear.Id);
                Assert.Greater(rod.Tip.x, rod.HandleEnd.x, gear.Id + " 손잡이(왼쪽 아래) → 끝(오른쪽 위)");
                Assert.Less(rod.Tip.y, rod.HandleEnd.y, gear.Id);
            }

            foreach (var gear in Gears(GearSlot.Boat))
            {
                Assert.IsTrue(GearAnchors.Boats.TryGetValue(gear.Id, out var boat), gear.Id);
                Assert.Greater(boat.DeckY, 0f, gear.Id);
                Assert.Greater(boat.BottomY, boat.DeckY, gear.Id);
            }

            foreach (var gear in Gears(GearSlot.Fisher))
            {
                Assert.IsTrue(GearAnchors.Fishers.TryGetValue(gear.Id, out var fisher), gear.Id);
                Assert.Greater(fisher.WaistY, 0f, gear.Id);
                Assert.Greater(fisher.Hand.x, 0f, gear.Id);
            }
        }

        [Test]
        public void Layout_FootBelowBoatMiddleAndRodGripInHands()
        {
            foreach (var boat in GearAnchors.Boats)
            {
                foreach (var fisher in GearAnchors.Fishers)
                {
                    foreach (var rod in GearAnchors.Rods)
                    {
                        var layout = AnglerRig.Compute(boat.Value, fisher.Value, rod.Value, GearAnchors.FisherFootY, AnglerRig.DefaultFootBelowMid);
                        var label = boat.Key + "/" + fisher.Key + "/" + rod.Key;
                        var middle = (boat.Value.Top + boat.Value.Bottom) * 0.5f;
                        var height = boat.Value.Bottom - boat.Value.Top;
                        var foot = GearAnchors.FisherFootY + layout.FisherOffsetY;
                        Assert.AreEqual(middle + height * 0.1f, foot, 1e-3f, label + " 발끝 = 보트 가운데 + 높이의 10%");
                        Assert.Greater(foot, middle, label + " 가운데보다 아래");
                        Assert.Less(foot, boat.Value.Bottom, label + " 보트 바닥보다는 위");
                        Assert.LessOrEqual(Vector2.Distance(layout.GripPx, layout.HandPx), 1f, label);
                        var grip = rod.Value.HandleEnd + (rod.Value.Tip - rod.Value.HandleEnd) * 0.25f;
                        Assert.AreEqual(layout.RodOffsetPx + grip, layout.GripPx, label);
                    }
                }
            }
        }

        [Test]
        public void Layout_TipConvertsToNomadLocal()
        {
            var boat = GearAnchors.Boats["boat_normal_01"];
            Assert.AreEqual(129f, boat.Top, "bbox y0");
            Assert.AreEqual(209f, boat.Bottom, "bbox y1");
            var layout = AnglerRig.Compute(boat, GearAnchors.Fishers["fisher_normal_01"], GearAnchors.Rods["rod_normal_01"], 210f, 0.1f);
            Assert.AreEqual(169f + 8f - 210f, layout.FisherOffsetY, 1e-4f, "가운데 169 + 높이 80의 10% - 발끝 210");
            var tip = AnglerRig.ToLocal(layout.TipPx);
            Assert.AreEqual((layout.TipPx.x - 128f) / 100f, tip.x, 1e-5f);
            Assert.AreEqual((128f - layout.TipPx.y) / 100f, tip.y, 1e-5f);
        }

        [Test]
        public void Visuals_DefaultsAndPathsForEverySlot()
        {
            GearBonuses.Clear();
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var id = GearVisuals.EquippedOrDefaultId(slot);
                Assert.AreEqual(slot.ToString().ToLowerInvariant() + "_normal_01", id);
                Assert.IsNotNull(GearVisuals.Load(GearVisuals.WorldPath(slot, id)), slot + " 외형");
                Assert.IsNotNull(GearVisuals.Load(GearVisuals.IconPath(slot, id)), slot + " 아이콘");
            }

            Assert.AreEqual(15, GearAnchors.LureEyes.Count, "루어 고리 위치");
        }

        [Test]
        public void Helmet_HeadTableFollowsDiverFrames()
        {
            var texture = new Texture2D(80, 80);
            try
            {
                var idle = Sprite.Create(texture, new Rect(0, 0, 80, 80), new Vector2(0.5f, 0.5f), 100f);
                idle.name = "player-idle_3";
                Assert.IsTrue(ScrapFishing.Dive.HelmetSkin.TryHead(idle, out var head));
                Assert.AreEqual(new Vector2(45.5f, 22f), head);
                idle.name = "player-swiming_5";
                Assert.IsTrue(ScrapFishing.Dive.HelmetSkin.TryHead(idle, out head));
                Assert.AreEqual(new Vector2(58f, 34f), head);
                idle.name = "player-hurt_2";
                Assert.IsFalse(ScrapFishing.Dive.HelmetSkin.TryHead(idle, out _), "피격 중에는 숨긴다");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void StarterGear_FillsOnlyEmptySlotsWithoutDuplicates()
        {
            var fresh = new GearInventory(_catalog);
            Assert.IsTrue(fresh.EnsureStarterGear());
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var id = slot.ToString().ToLowerInvariant() + "_normal_01";
                Assert.AreEqual(id, fresh.EquippedId(slot), slot.ToString());
                Assert.AreEqual(1, fresh.Owned(id), id + " 1개만");
            }

            Assert.IsFalse(fresh.EnsureStarterGear(), "다시 불러도 바뀌지 않는다");

            var existing = new GearInventory(_catalog);
            existing.AddPulled(new[] { _catalog.FindGear("lure_legend_01"), _catalog.FindGear("rod_normal_01"), _catalog.FindGear("boat_rare_01") }, "test", 0L);
            existing.Equip("lure_legend_01");
            existing.Store("boat_rare_01", 1);
            Assert.IsTrue(existing.EnsureStarterGear());
            Assert.AreEqual("lure_legend_01", existing.EquippedId(GearSlot.Lure), "이미 장착한 것은 그대로");
            Assert.AreEqual("rod_normal_01", existing.EquippedId(GearSlot.Rod));
            Assert.AreEqual(1, existing.Owned("rod_normal_01"), "갖고 있던 노멀 1번을 장착하고 새로 넣지 않는다");
            Assert.AreEqual("boat_normal_01", existing.EquippedId(GearSlot.Boat));
            Assert.AreEqual(1, existing.Count("boat_rare_01", GearState.Stored), "보관함의 다른 아이템은 그대로");
            Assert.AreEqual(0, existing.Owned("lure_normal_01"), "루어 칸은 이미 차 있어서 노멀을 넣지 않는다");
        }
    }
}
