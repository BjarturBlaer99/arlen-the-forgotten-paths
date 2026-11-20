using System.Collections;
using Arlen.AI;
using Arlen.Combat;
using Arlen.Player;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Arlen.Tests
{
    /// <summary>Builds minimal gameplay objects from components so PlayMode tests need no scenes or prefabs.</summary>
    public static class TestRig
    {
        public const string ActionsPath = "Assets/Arlen/Input/ArlenControls.inputactions";

        public static InputActionAsset LoadActions()
        {
#if UNITY_EDITOR
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            return source != null ? Object.Instantiate(source) : null; // isolate from the shared asset
#else
            return null;
#endif
        }

        /// <summary>Flat ground; optionally with a NavMesh baked immediately (before agents are created).</summary>
        public static GameObject CreateGround(bool withNavMesh)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            if (withNavMesh)
            {
                var surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
            }
            return ground;
        }

        /// <summary>Creates an inactive player; call SetActive(true) to start it.</summary>
        public static GameObject CreatePlayer(InputActionAsset actions, Vector3 position)
        {
            var player = new GameObject("TestPlayer");
            player.SetActive(false);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.minMoveDistance = 0f;
            player.AddComponent<PlayerInputReader>().SetActions(actions);
            player.AddComponent<PlayerActionState>();
            player.AddComponent<HealthComponent>();
            player.AddComponent<StaminaComponent>();
            player.AddComponent<ArlenPlayerController>();
            player.AddComponent<PlayerDodge>();
            player.AddComponent<PlayerMeleeCombat>();
            player.AddComponent<PlayerParry>();
            player.AddComponent<PlayerHitReaction>();
            player.AddComponent<Targetable>().Configure(Team.Player);
            player.transform.position = position;
            return player;
        }

        public static EnemyStats CreateBoarStats()
        {
            var stats = ScriptableObject.CreateInstance<EnemyStats>();
            stats.displayName = "Test Boar";
            stats.wanderRadius = 0f;      // stand still until engaged
            stats.alertDelay = 0.1f;
            stats.attackCooldown = new Vector2(0.3f, 0.3f);
            return stats;
        }

        /// <summary>Creates an inactive Boar Ruffian (logic only, no visuals) facing the given direction.</summary>
        public static GameObject CreateBoar(EnemyStats stats, Vector3 position, Vector3 facing)
        {
            var boar = new GameObject("TestBoar");
            boar.SetActive(false);
            boar.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            var agent = boar.AddComponent<NavMeshAgent>();
            agent.radius = 0.55f;
            agent.height = 1.9f;
            var capsule = boar.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.95f, 0f);
            capsule.height = 1.9f;
            capsule.radius = 0.55f;
            boar.AddComponent<HealthComponent>();
            boar.AddComponent<Targetable>().Configure(Team.Enemy);
            boar.AddComponent<EnemySensor>();
            boar.AddComponent<EnemyMotor>();
            boar.AddComponent<EnemyMeleeAttack>();
            boar.AddComponent<EnemyController>().SetStats(stats);
            return boar;
        }

        public static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        /// <summary>Waits until the condition is true or the timeout passes. Check the condition afterwards.</summary>
        public static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
        }
    }
}
