using System.Collections;
using Arlen.AI;
using Arlen.Combat;
using Arlen.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Arlen.Tests
{
    /// <summary>
    /// Loads the generated prototype scene and checks it boots cleanly: player, camera, HUD and the
    /// Boar Ruffian on the NavMesh. Any error logged during load fails the test (Unity Test Framework default).
    /// </summary>
    public class PrototypeSceneSmokeTests
    {
        const string ScenePath = "Assets/Arlen/Scenes/Prototype_TrainingGround.unity";

        [UnityTest]
        public IEnumerator PrototypeSceneBootsWithPlayerAndBoar()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(ScenePath)))
                Assert.Ignore("Prototype scene not generated (Tools > Arlen > Create Prototype Scene).");

            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestRig.WaitSeconds(1f);

            var player = Object.FindAnyObjectByType<ArlenPlayerController>();
            Assert.IsNotNull(player, "Player missing.");
            Assert.IsTrue(player.IsGrounded, "Player should stand on the ground.");
            Assert.IsNotNull(player.GetComponent<PlayerParry>(), "Player prefab lacks parry (rerun the scene builder).");
            Assert.AreEqual(Team.Player, player.GetComponent<Targetable>().Team);
            Assert.IsNotNull(Camera.main, "Main camera missing.");

            var boar = Object.FindAnyObjectByType<EnemyController>();
            Assert.IsNotNull(boar, "Boar Ruffian missing.");
            Assert.IsTrue(boar.GetComponent<EnemyMotor>().IsReady, "Boar should be placed on the baked NavMesh.");
            Assert.AreEqual(EnemyState.Idle, boar.State, "Boar starts out of detection range.");

            Time.timeScale = 1f;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
