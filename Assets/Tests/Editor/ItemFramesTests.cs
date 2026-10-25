using System.Linq;
using NUnit.Framework;
using ScrapFishing.Data;
using UnityEditor;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class ItemFramesTests
    {
        ItemData[] Items()
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Resources/ItemDatabase.asset");
            Assert.IsNotNull(database, "ItemDatabase.asset");
            return database.Items.Where(item => item != null).ToArray();
        }

        static bool Animated(ItemData item)
        {
            return item.Rarity == ItemRarity.Epic || item.Rarity == ItemRarity.Legend || item.Rarity == ItemRarity.Synchro;
        }

        [Test]
        public void EpicAndAbove_HaveEightFrames()
        {
            var animated = Items().Where(Animated).ToArray();
            Assert.AreEqual(15, animated.Length, "에픽 이상 아이템 수");
            foreach (var item in animated)
            {
                Assert.AreEqual(8, item.Frames.Length, item.Id);
                Assert.IsTrue(item.Frames.All(frame => frame != null), item.Id + " null 프레임");
                Assert.IsTrue(item.HasFrames, item.Id);
                for (var i = 0; i < item.Frames.Length; i++)
                {
                    Assert.AreEqual(new Vector2(0.5f, 0.5f), item.Frames[i].pivot / item.Frames[i].rect.size, item.Id + " 피벗 중앙: " + i);
                }
            }
        }

        [Test]
        public void NormalAndRare_HaveNoFrames()
        {
            foreach (var item in Items().Where(item => !Animated(item)))
            {
                Assert.AreEqual(0, item.Frames.Length, item.Id);
                Assert.IsFalse(item.HasFrames, item.Id);
            }
        }

        [Test]
        public void Synchro_IsOnlyBladeGlassSnakeInAbyss()
        {
            var items = Items();
            var synchro = items.Where(item => item.Rarity == ItemRarity.Synchro).ToArray();
            Assert.AreEqual(1, synchro.Length);
            Assert.AreEqual("blade_glass_snake", synchro[0].Id);
            Assert.AreEqual("칼날비늘용", synchro[0].DisplayName);
            Assert.AreEqual("abyss", synchro[0].Zone);
            Assert.IsFalse(items.Any(item => item.Id.Contains("pegigas")));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/pegigas.asset"));
        }
    }
}
