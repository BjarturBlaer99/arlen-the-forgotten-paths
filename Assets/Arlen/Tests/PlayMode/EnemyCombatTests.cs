using System.Collections;
using Arlen.AI;
using Arlen.Combat;
using Arlen.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Arlen.Tests
{
    /// <summary>Milestone 2: the Boar Ruffian fights a player built from real components on a runtime NavMesh.</summary>
    public class EnemyCombatTests : InputTestFixture
    {
        Mouse mouse;
        InputActionAsset actions;
        GameObject ground, player, boar;
        EnemyStats stats;

        EnemyController Brain => boar.GetComponent<EnemyController>();
        EnemyMeleeAttack Attack => boar.GetComponent<EnemyMeleeAttack>();
        HealthComponent BoarHealth => boar.GetComponent<HealthComponent>();
        HealthComponent PlayerHealth => player.GetComponent<HealthComponent>();

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            actions = TestRig.LoadActions();
            Assert.IsNotNull(actions, "Missing input actions asset.");

            ground = TestRig.CreateGround(withNavMesh: true);
            player = TestRig.CreatePlayer(actions, new Vector3(0f, 0.05f, 0f));
            stats = TestRig.CreateBoarStats();
            // Boar 6 m in front of the player, facing it.
            boar = TestRig.CreateBoar(stats, new Vector3(0f, 0f, 6f), Vector3.back);
        }

        public override void TearDown()
        {
            Object.Destroy(boar);
            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(stats);
            if (actions != null) Object.Destroy(actions);
            base.TearDown();
        }

        IEnumerator StartAll()
        {
            player.SetActive(true);
            boar.SetActive(true);
            yield return TestRig.WaitSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator BoarIdlesWhenPlayerIsOutOfRange()
        {
            player.transform.position = new Vector3(0f, 0.05f, -30f);
            yield return StartAll();
            yield return TestRig.WaitSeconds(1f);
            Assert.AreEqual(EnemyState.Idle, Brain.State);
            Assert.IsNull(Brain.Target);
        }

        [UnityTest]
        public IEnumerator BoarDetectsAndChasesPlayer()
        {
            yield return StartAll();
            float startDistance = Vector3.Distance(boar.transform.position, player.transform.position);
            yield return TestRig.WaitUntil(() => Brain.State == EnemyState.Chase || Brain.State == EnemyState.Attack, 2f);
            Assert.That(Brain.State, Is.EqualTo(EnemyState.Chase).Or.EqualTo(EnemyState.Attack), "Boar should notice the player.");
            yield return TestRig.WaitSeconds(0.6f);
            float distance = Vector3.Distance(boar.transform.position, player.transform.position);
            Assert.That(distance, Is.LessThan(startDistance - 1.5f), "Boar should close the distance.");
        }

        [UnityTest]
        public IEnumerator BoarAttackDamagesAndStunsPlayer()
        {
            yield return StartAll();
            yield return TestRig.WaitUntil(() => PlayerHealth.Current < PlayerHealth.Max, 5f);
            Assert.AreEqual(PlayerHealth.Max - stats.attackDamage, PlayerHealth.Current, 0.01f, "One club hit should land.");
            Assert.AreEqual(PlayerAction.HitStun, player.GetComponent<PlayerActionState>().Current, "Heavy hit stuns the player.");
        }

        [UnityTest]
        public IEnumerator TimedParryDeflectsAndStaggersBoar()
        {
            yield return StartAll();
            // Wait for the telegraph to be nearly complete, then parry.
            yield return TestRig.WaitUntil(() => Attack.Phase == EnemyAttackPhase.Windup && Attack.PhaseProgress >= 0.8f, 5f);
            Assert.AreEqual(EnemyAttackPhase.Windup, Attack.Phase, "Boar never wound up an attack.");
            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);

            yield return TestRig.WaitUntil(() => Brain.State == EnemyState.Stagger, 1f);
            Assert.AreEqual(EnemyState.Stagger, Brain.State, "Parried boar should stagger.");
            Assert.IsTrue(Brain.LastStaggerFromParry);
            Assert.AreEqual(PlayerHealth.Max, PlayerHealth.Current, "Parried hit deals no damage.");
        }

        [UnityTest]
        public IEnumerator HeavyDamageBreaksPoiseAndStaggers()
        {
            yield return StartAll();
            BoarHealth.ApplyDamage(new DamageInfo(stats.poise + 1f, player, boar.transform.position, Vector3.forward));
            Assert.AreEqual(EnemyState.Stagger, Brain.State);
            Assert.IsFalse(Brain.LastStaggerFromParry);
        }

        [UnityTest]
        public IEnumerator LightHitsDoNotStaggerButAggro()
        {
            player.transform.position = new Vector3(0f, 0.05f, -30f); // out of sight
            yield return StartAll();
            BoarHealth.ApplyDamage(new DamageInfo(stats.poise * 0.4f, player, boar.transform.position, Vector3.forward));
            Assert.AreEqual(EnemyState.Chase, Brain.State, "Being hit makes the boar chase the attacker.");
            Assert.AreEqual(player.GetComponent<Targetable>(), Brain.Target);
        }

        [UnityTest]
        public IEnumerator BoarDiesAndCanBeReset()
        {
            yield return StartAll();
            bool died = false;
            Brain.Died += () => died = true;
            for (int i = 0; i < 20 && BoarHealth.IsAlive; i++)
                BoarHealth.ApplyDamage(new DamageInfo(15f, player, boar.transform.position, Vector3.forward));

            Assert.IsTrue(died);
            Assert.AreEqual(EnemyState.Dead, Brain.State);
            Assert.IsFalse(boar.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "Navigation stops on death.");

            yield return TestRig.WaitSeconds(0.5f);
            Assert.That(PlayerHealth.Current, Is.EqualTo(PlayerHealth.Max), "Dead boar must not attack.");

            Brain.ResetToHome();
            Assert.AreEqual(EnemyState.Idle, Brain.State);
            Assert.AreEqual(BoarHealth.Max, BoarHealth.Current);
        }

        [UnityTest]
        public IEnumerator PlayerComboKillsBoar()
        {
            // Player swings at a boar standing in front of them until it falls.
            boar.transform.position = new Vector3(0f, 0f, 1.8f);
            yield return StartAll();
            for (int i = 0; i < 30 && BoarHealth.IsAlive; i++)
            {
                Press(mouse.leftButton);
                yield return null;
                Release(mouse.leftButton);
                yield return TestRig.WaitSeconds(0.3f);
                // Keep the test about our damage output: top the player up if the boar lands a hit.
                if (!PlayerHealth.IsAlive) break;
                PlayerHealth.Heal(100f);
            }
            Assert.IsFalse(BoarHealth.IsAlive, "Sword attacks should eventually kill the boar.");
        }
    }
}
