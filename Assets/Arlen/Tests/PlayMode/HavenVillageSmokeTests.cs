using System.Collections;
using System.Linq;
using Arlen.AI;
using Arlen.Interaction;
using Arlen.NPC;
using Arlen.Player;
using Arlen.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Arlen.Tests
{
    /// <summary>Loads the generated Haven Village and checks the exploration slice is wired up.</summary>
    public class HavenVillageSmokeTests
    {
        const string ScenePath = "Assets/Arlen/Scenes/Haven_Village.unity";

        [UnityTest]
        public IEnumerator HavenVillageBootsWithCabinVillagersAndForest()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(ScenePath)))
                Assert.Ignore("Haven Village not generated (Tools > Arlen > Create Haven Village Scene).");

            // Never read or write the player's real save from a test.
            string savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"arlen_smoke_{System.Guid.NewGuid():N}.json");
            Saving.SaveManager.PathOverride = savePath;
            try
            {
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return TestRig.WaitSeconds(1f);

                var player = Object.FindAnyObjectByType<ArlenPlayerController>();
                Assert.IsNotNull(player, "Player missing.");
                Assert.IsTrue(player.IsGrounded, "Player should stand on the porch.");
                var interactor = player.GetComponent<PlayerInteractor>();
                Assert.IsNotNull(interactor, "Player lacks PlayerInteractor (rerun the builder).");
                Assert.IsNotNull(interactor.Dialogue, "Interactor has no DialogueRunner.");
                Assert.IsNotNull(interactor.ScreenFader, "Interactor has no ScreenFader.");

                var npcs = Object.FindObjectsByType<NpcCharacter>();
                CollectionAssert.AreEquivalent(new[] { "Bram", "Mabel" }, npcs.Select(n => n.DisplayName).ToArray());

                var doors = Object.FindObjectsByType<Door>();
                Assert.IsTrue(doors.Any(d => !d.IsLocked), "The cabin door should open.");
                Assert.IsNotNull(Object.FindAnyObjectByType<RestSpot>(), "Cabin bed missing.");
                Assert.IsNotNull(Object.FindAnyObjectByType<StorageChest>(), "Cabin chest missing.");
                Assert.That(Object.FindObjectsByType<Examinable>().Length, Is.GreaterThanOrEqualTo(8));
                Assert.IsNotNull(Object.FindAnyObjectByType<InteriorZone>(), "Cabin interior zone missing.");
                Assert.AreEqual(3, Object.FindObjectsByType<AreaZone>().Length, "Village, Mosswood and the Waybell shrine.");

                var boar = Object.FindAnyObjectByType<EnemyController>();
                Assert.IsNotNull(boar, "Mosswood boar missing.");
                Assert.IsTrue(boar.GetComponent<EnemyMotor>().IsReady, "Boar must be on the NavMesh.");
                Assert.AreEqual(EnemyState.Idle, boar.State, "Boar shouldn't notice a player in the village.");

                // Milestone 4: RPG systems
                Assert.IsNotNull(player.GetComponent<Items.PlayerInventory>(), "Player lacks PlayerInventory.");
                var equipment = player.GetComponent<Items.PlayerEquipment>();
                Assert.IsNotNull(equipment, "Player lacks PlayerEquipment.");
                Assert.IsNotNull(equipment.Weapon, "Player should start with a sword equipped.");
                Assert.IsNotNull(player.GetComponent<Saving.PlayerSaveState>());
                Assert.IsNotNull(interactor.Quests, "Interactor has no quest service.");
                Assert.IsNotNull(interactor.InventoryScreen, "Interactor has no inventory screen.");
                Assert.IsNotNull(Object.FindAnyObjectByType<Saving.SaveManager>(), "SaveManager missing.");

                var chest = Object.FindAnyObjectByType<StorageChest>();
                Assert.IsTrue(chest.HasStorage, "Cabin chest should store items.");
                Assert.Greater(chest.Contents.SlotsUsed, 0, "Cabin chest should start with supplies.");
                var pickups = Object.FindObjectsByType<Items.ItemPickup>();
                Assert.GreaterOrEqual(pickups.Length, 5);
                Assert.IsTrue(pickups.All(p => p.Item != null && !string.IsNullOrEmpty(p.PickupId)), "Every pickup needs an item and an id.");
                Assert.AreEqual(pickups.Length, pickups.Select(p => p.PickupId).Distinct().Count(), "Pickup ids must be unique.");
                var bell = Object.FindAnyObjectByType<Waybell>();
                Assert.IsNotNull(bell, "Waybell missing.");
                Assert.IsFalse(bell.IsAwake);
                Assert.AreEqual(1, Object.FindObjectsByType<Examinable>().Count(e => e.ExamineId == "vanished_road"));

                var questManager = Object.FindAnyObjectByType<Quests.QuestManager>();
                yield return TestRig.WaitUntil(() => questManager.Log.Tracked != null, 5f);
                Assert.IsNotNull(questManager.Log.Tracked, "The Road That Vanished should start on a new game.");
                Assert.AreEqual("road_that_vanished", questManager.Log.Tracked.Id);
                Assert.IsTrue(questManager.IsObjectiveActive("road_that_vanished", "ask_about_road"));
                Assert.IsFalse(System.IO.File.Exists(savePath), "Nothing should be saved before resting.");
            }
            finally
            {
                Saving.SaveManager.PathOverride = null;
            }
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
