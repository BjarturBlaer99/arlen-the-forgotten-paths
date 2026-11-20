using System.Collections;
using Arlen.DialogueSystem;
using Arlen.Interaction;
using Arlen.NPC;
using Arlen.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Arlen.Tests
{
    /// <summary>Milestone 3: pressing E on doors, signs and villagers with simulated keyboard input.</summary>
    public class InteractionPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        InputActionAsset actions;
        GameObject ground, player, services;
        DialogueRunner runner;

        PlayerInteractor Interactor => player.GetComponent<PlayerInteractor>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            actions = TestRig.LoadActions();
            ground = TestRig.CreateGround(withNavMesh: false);
            player = TestRig.CreatePlayer(actions, new Vector3(0f, 0.05f, 0f));
            player.AddComponent<PlayerInteractor>();

            services = new GameObject("Services");
            runner = services.AddComponent<DialogueRunner>();
            runner.Configure(player.GetComponent<PlayerInputReader>(), null);
            player.GetComponent<PlayerInteractor>().SetServices(runner, null);
        }

        public override void TearDown()
        {
            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(services);
            if (actions != null) Object.Destroy(actions);
            base.TearDown();
        }

        IEnumerator Start()
        {
            player.SetActive(true);
            yield return TestRig.WaitSeconds(0.25f);
        }

        IEnumerator PressE()
        {
            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
        }

        static GameObject CreateDoor(Vector3 position, bool locked)
        {
            var root = new GameObject("TestDoor");
            root.SetActive(false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // facing the player
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(1.4f, 2f, 0.8f);
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-0.7f, 0f, 0f);
            var door = root.AddComponent<Door>();
            door.IsLocked = locked;
            // Assign the private hinge field the same way the builder does.
            typeof(Door).GetField("hinge", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(door, hinge);
            root.SetActive(true);
            return root;
        }

        [UnityTest]
        public IEnumerator FocusesDoorInFrontAndOpensItWithE()
        {
            var doorGo = CreateDoor(new Vector3(0f, 0f, 1.6f), locked: false);
            try
            {
                yield return Start();
                Assert.AreSame(doorGo.GetComponent<Door>(), Interactor.Focus, "Door in front should be focused.");
                Assert.AreEqual("Open", Interactor.Focus.InteractionPrompt);

                yield return PressE();
                var door = doorGo.GetComponent<Door>();
                Assert.IsTrue(door.IsOpen);
                yield return TestRig.WaitSeconds(0.6f);
                Assert.That(Mathf.Abs(door.CurrentAngle), Is.GreaterThan(90f), "Door should swing open.");

                yield return TestRig.WaitSeconds(0.1f);
                yield return PressE();
                Assert.IsFalse(door.IsOpen, "Second press closes it.");
            }
            finally
            {
                Object.Destroy(doorGo);
            }
        }

        [UnityTest]
        public IEnumerator NothingFocusedWhenOutOfReach()
        {
            var doorGo = CreateDoor(new Vector3(0f, 0f, 6f), locked: false);
            try
            {
                yield return Start();
                Assert.IsNull(Interactor.Focus);
            }
            finally
            {
                Object.Destroy(doorGo);
            }
        }

        [UnityTest]
        public IEnumerator LockedDoorShowsMessageAndBlocksMovementUntilDismissed()
        {
            var doorGo = CreateDoor(new Vector3(0f, 0f, 1.6f), locked: true);
            try
            {
                yield return Start();
                yield return PressE();
                Assert.IsTrue(runner.IsRunning, "Locked door shows its message.");
                Assert.IsFalse(doorGo.GetComponent<Door>().IsOpen);
                Assert.IsNull(Interactor.Focus, "No new focus while reading.");

                // Movement is blocked while the dialogue box is up.
                Vector3 before = player.transform.position;
                Press(keyboard.sKey);
                yield return TestRig.WaitSeconds(0.3f);
                Release(keyboard.sKey);
                yield return null;
                Assert.Less(Vector3.Distance(before, player.transform.position), 0.05f, "Player must not move during dialogue.");

                // Complete the typewriter if needed, then dismiss. The closing press must not re-open it.
                if (!runner.LineFullyRevealed) yield return PressE();
                yield return PressE();
                yield return null;
                Assert.IsFalse(runner.IsRunning, "Closing press must not trigger the door again.");
                Assert.IsFalse(player.GetComponent<PlayerInputReader>().GameplayBlocked);
            }
            finally
            {
                Object.Destroy(doorGo);
            }
        }

        [UnityTest]
        public IEnumerator TalkingToNpcPlaysWholeConversation()
        {
            var first = ScriptableObject.CreateInstance<DialogueData>();
            first.defaultSpeaker = "Mabel";
            first.lines = new[]
            {
                new DialogueData.Line("", "Look who's back!"),
                new DialogueData.Line("You", "Hello, Mabel."),
                new DialogueData.Line("", "Go see Eldon."),
            };
            var npcGo = new GameObject("TestNPC");
            npcGo.SetActive(false);
            npcGo.transform.position = new Vector3(0f, 0f, 1.8f);
            var capsule = npcGo.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            var npc = npcGo.AddComponent<NpcCharacter>();
            npc.Configure("Mabel", first);
            npcGo.SetActive(true);
            try
            {
                yield return Start();
                Assert.AreSame(npc, Interactor.Focus);
                Assert.AreEqual("Talk", Interactor.Focus.InteractionPrompt);

                yield return PressE();
                Assert.IsTrue(runner.IsRunning);
                Assert.IsTrue(npc.IsTalking);
                Assert.AreEqual("Mabel", runner.CurrentSpeaker);

                for (int i = 0; i < 10 && runner.IsRunning; i++)
                {
                    if (runner.LineIndex == 1) Assert.AreEqual("You", runner.CurrentSpeaker);
                    yield return PressE();
                }
                Assert.IsFalse(runner.IsRunning, "Conversation should end after its lines.");
                Assert.IsTrue(npc.HasMet);
                Assert.AreEqual(1, npc.ConversationsHad);
            }
            finally
            {
                Object.Destroy(npcGo);
                Object.Destroy(first);
            }
        }
    }
}
