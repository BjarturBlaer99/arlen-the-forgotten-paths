using System.Collections;
using Arlen.Combat;
using Arlen.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace Arlen.Tests
{
    /// <summary>
    /// End-to-end player tests using simulated keyboard/mouse devices. They build a minimal player
    /// from components (no prefab or scene required) so they also run in a clean checkout.
    /// </summary>
    public class PlayerGameplayTests : InputTestFixture
    {
        const string ActionsPath = "Assets/Arlen/Input/ArlenControls.inputactions";

        Keyboard keyboard;
        Mouse mouse;
        GameObject ground;
        GameObject player;
        InputActionAsset actions;

        ArlenPlayerController Controller => player.GetComponent<ArlenPlayerController>();
        StaminaComponent Stamina => player.GetComponent<StaminaComponent>();
        HealthComponent Health => player.GetComponent<HealthComponent>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

#if UNITY_EDITOR
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.IsNotNull(source, $"Missing {ActionsPath}");
            actions = Object.Instantiate(source); // isolate from the shared asset
#endif

            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);

            player = new GameObject("TestPlayer");
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
            player.transform.position = new Vector3(0f, 0.1f, 0f);
        }

        public override void TearDown()
        {
            Object.Destroy(player);
            Object.Destroy(ground);
            if (actions != null) Object.Destroy(actions);
            base.TearDown();
        }

        IEnumerator Spawn()
        {
            player.SetActive(true);
            // Let the controller settle onto the ground (frames can be extremely short in batch mode).
            float minEnd = Time.time + 0.2f;
            float timeout = Time.time + 1f;
            while (Time.time < timeout && (Time.time < minEnd || !Controller.IsGrounded)) yield return null;
        }

        // InputTestFixture sends a full device snapshot per Press/Release, so two calls in the same
        // frame overwrite each other. Always advance a frame between key changes.
        IEnumerator Hold(ButtonControl key)
        {
            Press(key);
            yield return null;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerLandsOnGround()
        {
            player.transform.position = new Vector3(0f, 2f, 0f);
            yield return Spawn();
            yield return WaitSeconds(1f);
            Assert.IsTrue(Controller.IsGrounded, "Player should be grounded after falling.");
            Assert.That(player.transform.position.y, Is.InRange(-0.05f, 0.2f));
        }

        [UnityTest]
        public IEnumerator HoldingWMovesForward()
        {
            yield return Spawn();
            Vector3 start = player.transform.position;
            Press(keyboard.wKey);
            yield return WaitSeconds(0.5f);
            Release(keyboard.wKey);
            float moved = player.transform.position.z - start.z;
            Assert.That(moved, Is.GreaterThan(1.5f), $"Expected forward movement, moved {moved:F2} m");
            Assert.That(Mathf.Abs(player.transform.position.x - start.x), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator SpaceJumps()
        {
            yield return Spawn();
            float startY = player.transform.position.y;
            PressAndRelease(keyboard.spaceKey);
            float maxY = startY;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                maxY = Mathf.Max(maxY, player.transform.position.y);
                yield return null;
            }
            Assert.That(maxY - startY, Is.GreaterThan(0.8f), "Jump should lift the player about 1.35 m.");
            yield return WaitSeconds(1f);
            Assert.IsTrue(Controller.IsGrounded, "Player should land again.");
        }

        [UnityTest]
        public IEnumerator SprintIsFasterAndDrainsStamina()
        {
            yield return Spawn();
            yield return Hold(keyboard.wKey);
            yield return Hold(keyboard.leftShiftKey);
            yield return WaitSeconds(0.6f);
            Assert.IsTrue(Controller.IsSprinting);
            Assert.That(Controller.PlanarSpeed, Is.GreaterThan(Controller.RunSpeed + 0.5f));
            Assert.That(Stamina.Current, Is.LessThan(Stamina.Max));
            Release(keyboard.leftShiftKey);
            yield return null;
            Release(keyboard.wKey);
        }

        [UnityTest]
        public IEnumerator DodgeCostsStaminaMovesAndGrantsInvulnerability()
        {
            yield return Spawn();
            var dodge = player.GetComponent<PlayerDodge>();
            Vector3 start = player.transform.position;

            yield return Hold(keyboard.dKey);
            yield return Hold(keyboard.leftAltKey);
            Release(keyboard.leftAltKey);
            yield return null;
            Assert.IsTrue(dodge.IsDodging, "Dodge should start.");
            Release(keyboard.dKey);

            yield return WaitSeconds(dodge.Duration * 0.3f);
            Assert.IsTrue(Health.IsInvulnerable, "Should be invulnerable mid-dodge.");
            bool hurt = Health.ApplyDamage(new DamageInfo(10f, null, Vector3.zero, Vector3.forward));
            Assert.IsFalse(hurt, "Damage during i-frames should be ignored.");
            Assert.AreEqual(Health.Max, Health.Current);

            yield return WaitSeconds(dodge.Duration);
            Assert.IsFalse(dodge.IsDodging);
            Assert.IsFalse(Health.IsInvulnerable);
            Assert.That(Stamina.Current, Is.LessThan(Stamina.Max));
            Assert.That(player.transform.position.x - start.x, Is.GreaterThan(2.5f), "Dodge right should travel several metres.");
        }

        [UnityTest]
        public IEnumerator AttackDamagesTargetInFront()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.transform.position = new Vector3(0f, 1f, 1.4f);
            var targetHealth = target.AddComponent<HealthComponent>();
            try
            {
                yield return Spawn();
                PressAndRelease(mouse.leftButton);
                yield return WaitSeconds(0.4f);
                Assert.That(targetHealth.Current, Is.LessThan(targetHealth.Max), "First combo hit should land.");
                Assert.That(Stamina.Current, Is.LessThan(Stamina.Max), "Attacks cost stamina.");
            }
            finally
            {
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator ThreeClicksPerformFullCombo()
        {
            yield return Spawn();
            var combat = player.GetComponent<PlayerMeleeCombat>();
            int maxStep = -1;
            combat.AttackStarted += (i, _) => maxStep = Mathf.Max(maxStep, i);

            for (int i = 0; i < 3; i++)
            {
                PressAndRelease(mouse.leftButton);
                yield return WaitSeconds(0.28f);
            }
            yield return WaitSeconds(1.2f);
            Assert.AreEqual(2, maxStep, "All three combo steps should play.");
            Assert.IsFalse(combat.IsAttacking, "Combo should finish.");
        }

        [UnityTest]
        public IEnumerator TakingLethalDamageKillsPlayer()
        {
            yield return Spawn();
            bool died = false;
            Health.Died += () => died = true;
            Health.ApplyDamage(new DamageInfo(Health.Max + 1f, null, Vector3.zero, Vector3.forward));
            Assert.IsTrue(died);
            Assert.IsFalse(Health.IsAlive);
        }
    }
}
