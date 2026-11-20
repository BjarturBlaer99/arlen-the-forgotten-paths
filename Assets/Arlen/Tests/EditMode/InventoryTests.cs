using System.Collections.Generic;
using Arlen.Items;
using Arlen.Saving;
using Arlen.UI;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class InventoryTests
    {
        readonly List<Object> created = new List<Object>();
        ItemDefinition herb, coin;
        EquipmentDefinition sword;

        [SetUp]
        public void SetUp()
        {
            herb = Item<ItemDefinition>("sunmoss", 10);
            coin = Item<ItemDefinition>("coin", 99);
            sword = Item<EquipmentDefinition>("sword", 1);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        T Item<T>(string id, int maxStack) where T : ItemDefinition
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.id = id;
            item.displayName = id;
            item.maxStack = maxStack;
            created.Add(item);
            return item;
        }

        [Test]
        public void StackableItemsFillStacksBeforeNewSlots()
        {
            var inv = new Inventory(4);
            Assert.AreEqual(7, inv.Add(herb, 7));
            Assert.AreEqual(8, inv.Add(herb, 8));
            Assert.AreEqual(2, inv.SlotsUsed, "15 herbs with stacks of 10 need two slots.");
            Assert.AreEqual(15, inv.CountOf(herb));
        }

        [Test]
        public void AddStopsAtCapacityAndReportsWhatFit()
        {
            var inv = new Inventory(2);
            Assert.AreEqual(1, inv.Add(sword));
            Assert.AreEqual(10, inv.Add(herb, 25), "Only one slot of herbs is left.");
            Assert.IsTrue(inv.IsFull);
            Assert.AreEqual(0, inv.Add(coin, 1));
            Assert.AreEqual(0, inv.SpaceFor(coin));
            Assert.AreEqual(0, inv.SpaceFor(herb));
        }

        [Test]
        public void RemoveTakesFromStacksAndFreesEmptySlots()
        {
            var inv = new Inventory(4);
            inv.Add(herb, 15);
            Assert.AreEqual(6, inv.Remove(herb, 6));
            Assert.AreEqual(9, inv.CountOf(herb));
            Assert.AreEqual(1, inv.SlotsUsed);
            Assert.AreEqual(9, inv.Remove(herb, 50), "Can't remove more than is there.");
            Assert.AreEqual(0, inv.SlotsUsed);
            Assert.IsFalse(inv.Contains(herb));
        }

        [Test]
        public void ChangedFiresOnlyForRealChanges()
        {
            var inv = new Inventory(1);
            int changes = 0;
            inv.Changed += () => changes++;
            inv.Add(herb, 3);
            inv.Add(null, 3);
            inv.Add(herb, 0);
            inv.Remove(coin, 1);
            inv.Add(coin, 1); // full: nothing added
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void TransferMovesOnlyWhatFits()
        {
            var chest = new Inventory(4);
            var pack = new Inventory(1);
            chest.Add(herb, 14);
            Assert.AreEqual(10, chest.TransferAll(herb, pack));
            Assert.AreEqual(10, pack.CountOf(herb));
            Assert.AreEqual(4, chest.CountOf(herb));
            Assert.AreEqual(0, chest.TransferAll(herb, pack), "Pack is full.");
            Assert.AreEqual(0, chest.TransferAll(herb, chest), "Can't transfer into itself.");
        }

        [Test]
        public void CaptureRestoreRoundTripSkipsUnknownItems()
        {
            var inv = new Inventory(6);
            inv.Add(herb, 12);
            inv.Add(sword);
            inv.Add(coin, 40);
            var saved = inv.Capture();
            saved.Add(new ItemSaveEntry("removed_item", 3));

            var lookup = new Dictionary<string, ItemDefinition> { { "sunmoss", herb }, { "sword", sword }, { "coin", coin } };
            var restored = new Inventory(6);
            restored.Restore(saved, id => lookup.TryGetValue(id, out var item) ? item : null);
            Assert.AreEqual(12, restored.CountOf(herb));
            Assert.AreEqual(1, restored.CountOf(sword));
            Assert.AreEqual(40, restored.CountOf(coin));
            Assert.AreEqual(4, restored.SlotsUsed);
        }

        [Test]
        public void ItemDatabaseFindsById()
        {
            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            created.Add(db);
            db.SetItems(new ItemDefinition[] { herb, sword });
            Assert.AreSame(sword, db.Find("sword"));
            Assert.IsNull(db.Find("nope"));
            Assert.IsNull(db.Find(null));
        }

        [Test]
        public void EquipmentDescribesItsStats()
        {
            sword.damageMultiplier = 1.25f;
            sword.maxStaminaBonus = 20f;
            StringAssert.Contains("Damage ×1.25", sword.StatsText);
            StringAssert.Contains("Max stamina +20", sword.StatsText);
            Assert.IsFalse(sword.IsUsable);

            herb.category = ItemCategory.Consumable;
            herb.healAmount = 20f;
            Assert.IsTrue(herb.IsUsable);
            Assert.AreEqual("Restores 20 health", herb.StatsText);
        }

        [TestCase("Pathwarden Sabre", "PS")]
        [TestCase("Sunmoss", "Su")]
        [TestCase("Honey-Cinnamon Roll", "HR")]
        [TestCase("", "?")]
        public void SlotInitials(string name, string expected) => Assert.AreEqual(expected, ItemSlotView.Abbreviate(name));
    }
}
