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
        public void Layout_FisherOnDeckAndRodGripInHands()
        {
            foreach (var boat in GearAnchors.Boats)
            {
                foreach (var fisher in GearAnchors.Fishers)
                {
                    foreach (var rod in GearAnchors.Rods)
                    {
                        var layout = AnglerRig.Compute(boat.Value, fisher.Value, rod.Value);
                        var label = boat.Key + "/" + fisher.Key + "/" + rod.Key;
                        Assert.AreEqual(boat.Value.DeckY - fisher.Value.WaistY, layout.FisherOffsetY, 1e-4f, label);
                        Assert.AreEqual(boat.Value.BottomY, layout.FisherClipY + layout.FisherOffsetY, 1e-4f, label + " 선체 바닥 아래로 다리가 나오지 않는다");
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
            var layout = AnglerRig.Compute(GearAnchors.Boats["boat_normal_01"], GearAnchors.Fishers["fisher_normal_01"], GearAnchors.Rods["rod_normal_01"]);
            Assert.AreEqual(-21f, layout.FisherOffsetY, 1e-4f, "deckY 129 - waistY 150");
            var tip = AnglerRig.ToLocal(layout.TipPx);
            Assert.AreEqual((layout.TipPx.x - 128f) / 100f, tip.x, 1e-5f);
            Assert.AreEqual((128f - layout.TipPx.y) / 100f, tip.y, 1e-5f);
        }
    }
}
