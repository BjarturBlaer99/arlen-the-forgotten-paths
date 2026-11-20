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
            Assert.AreEqual(2, Object.FindObjectsByType<AreaZone>().Length);

            var boar = Object.FindAnyObjectByType<EnemyController>();
            Assert.IsNotNull(boar, "Mosswood boar missing.");
            Assert.IsTrue(boar.GetComponent<EnemyMotor>().IsReady, "Boar must be on the NavMesh.");
            Assert.AreEqual(EnemyState.Idle, boar.State, "Boar shouldn't notice a player in the village.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
