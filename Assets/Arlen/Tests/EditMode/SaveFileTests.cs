using System.IO;
using Arlen.Quests;
using Arlen.Saving;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class SaveFileTests
    {
        string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(), $"arlen_test_{System.Guid.NewGuid():N}", "save.json");

        [TearDown]
        public void TearDown()
        {
            string dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }

        static SaveData Sample()
        {
            var data = new SaveData { sceneName = "Haven_Village" };
            data.player.hasPosition = true;
            data.player.position = new Vector3(1f, 2f, 3f);
            data.player.yaw = 90f;
            data.player.healthFraction = 0.5f;
            data.quests.Add(new QuestSaveEntry { questId = "road", status = QuestStatus.Active, objectiveIndex = 2, progress = 0 });
            data.questFacts.Add("Activated:bell");
            data.inventory.Add(new ItemSaveEntry("sunmoss", 3));
            data.equipment = new EquipmentSaveData { hasData = true, weaponId = "sabre", charmId = "token" };
            data.containers.Add(new ContainerSaveData { id = "cabin_chest", items = { new ItemSaveEntry("rope", 1) } });
            data.collectedPickups.Add("sunmoss_shrine");
            data.npcs.Add(new NpcSaveData { id = "Mabel", hasMet = true, repeatIndex = 2, playedQuestConversations = { 0 } });
            data.activatedWaybells.Add("waybell_mosswood");
            return data;
        }

        [Test]
        public void WriteThenReadRoundTripsEverySection()
        {
            SaveFile.Write(path, Sample());
            Assert.AreEqual(SaveReadResult.Ok, SaveFile.Read(path, out var data));

            Assert.AreEqual("Haven_Village", data.sceneName);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), data.player.position);
            Assert.AreEqual(0.5f, data.player.healthFraction);
            Assert.AreEqual(2, data.quests[0].objectiveIndex);
            Assert.AreEqual(QuestStatus.Active, data.quests[0].status);
            CollectionAssert.AreEqual(new[] { "Activated:bell" }, data.questFacts);
            Assert.AreEqual("sunmoss", data.inventory[0].itemId);
            Assert.AreEqual(3, data.inventory[0].count);
            Assert.AreEqual("sabre", data.equipment.weaponId);
            Assert.AreEqual("rope", data.FindContainer("cabin_chest").items[0].itemId);
            CollectionAssert.Contains(data.collectedPickups, "sunmoss_shrine");
            Assert.AreEqual(2, data.FindNpc("Mabel").repeatIndex);
            CollectionAssert.AreEqual(new[] { 0 }, data.FindNpc("Mabel").playedQuestConversations);
            CollectionAssert.Contains(data.activatedWaybells, "waybell_mosswood");
        }

        [Test]
        public void OverwritingKeepsOnlyTheNewSaveAndNoTempFiles()
        {
            SaveFile.Write(path, Sample());
            var second = Sample();
            second.player.healthFraction = 0.9f;
            SaveFile.Write(path, second);

            Assert.AreEqual(SaveReadResult.Ok, SaveFile.Read(path, out var data));
            Assert.AreEqual(0.9f, data.player.healthFraction);
            Assert.IsFalse(File.Exists(path + ".tmp"));
            Assert.IsFalse(File.Exists(path + ".bak"));
        }

        [Test]
        public void MissingCorruptAndFutureSavesAreReported()
        {
            Assert.AreEqual(SaveReadResult.Missing, SaveFile.Read(path, out _));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{ this is not json");
            Assert.AreEqual(SaveReadResult.Corrupt, SaveFile.Read(path, out var corrupt));
            Assert.IsNull(corrupt);

            File.WriteAllText(path, "");
            Assert.AreEqual(SaveReadResult.Corrupt, SaveFile.Read(path, out _));

            var future = Sample();
            future.version = SaveData.CurrentVersion + 1;
            SaveFile.Write(path, future);
            Assert.AreEqual(SaveReadResult.TooNew, SaveFile.Read(path, out var tooNew));
            Assert.IsNull(tooNew);
        }

        [Test]
        public void DeleteRemovesTheSave()
        {
            SaveFile.Write(path, Sample());
            Assert.IsTrue(SaveFile.Exists(path));
            SaveFile.Delete(path);
            Assert.IsFalse(SaveFile.Exists(path));
            Assert.DoesNotThrow(() => SaveFile.Delete(path));
        }
    }
}
