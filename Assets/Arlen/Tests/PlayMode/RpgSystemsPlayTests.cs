using System.Collections;
using System.Collections.Generic;
using System.IO;
using Arlen.AI;
using Arlen.Combat;
using Arlen.Core;
using Arlen.DialogueSystem;
using Arlen.Interaction;
using Arlen.Items;
using Arlen.NPC;
using Arlen.Player;
using Arlen.Quests;
using Arlen.Saving;
using Arlen.UI;
using Arlen.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Arlen.Tests
{
    /// <summary>Milestone 4: pickups, pack and chest screens, equipment, quests, the Waybell and saving, with simulated input.</summary>
    public class RpgSystemsPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        InputActionAsset actions;
        GameObject ground, player, services;
        readonly List<Object> spawned = new List<Object>();
        string savePath;

        DialogueRunner runner;
        QuestManager quests;
        InventoryScreen screen;
        PauseController pause;
        SaveManager saves;
        ItemDatabase database;
        ItemDefinition herb;
        EquipmentDefinition blade, sabre, token;

        PlayerInteractor Interactor => player.GetComponent<PlayerInteractor>();
        PlayerInventory Pack => player.GetComponent<PlayerInventory>();
        PlayerEquipment Equipment => player.GetComponent<PlayerEquipment>();
        PlayerInputReader Input => player.GetComponent<PlayerInputReader>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            savePath = Path.Combine(Path.GetTempPath(), $"arlen_playtest_{System.Guid.NewGuid():N}.json");
            SaveManager.PathOverride = savePath;

            herb = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            herb.id = "sunmoss";
            herb.displayName = "Sunmoss";
            herb.category = ItemCategory.Consumable;
            herb.healAmount = 20f;
            herb.maxStack = 10;
            blade = Gear("blade", EquipmentSlot.Weapon, 1f, 0f);
            sabre = Gear("sabre", EquipmentSlot.Weapon, 1.25f, 0f);
            token = Gear("token", EquipmentSlot.Charm, 1f, 20f);
            database = Track(ScriptableObject.CreateInstance<ItemDatabase>());
            database.SetItems(new ItemDefinition[] { herb, blade, sabre, token });

            actions = TestRig.LoadActions();
            ground = TestRig.CreateGround(withNavMesh: false);
            player = TestRig.CreatePlayer(actions, new Vector3(0f, 0.05f, 0f));
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerEquipment>().SetStartingGear(blade, null);
            player.AddComponent<PlayerSaveState>();

            services = new GameObject("Services");
            services.SetActive(false);
            runner = services.AddComponent<DialogueRunner>();
            runner.Configure(Input, null);
            quests = services.AddComponent<QuestManager>();
            quests.Configure(new QuestDefinition[0], new QuestDefinition[0]);
            saves = services.AddComponent<SaveManager>();
            saves.Configure(database, restoreOnStart: false);
            pause = services.AddComponent<PauseController>();
            TestRig.SetField(pause, "input", Input);
            screen = services.AddComponent<InventoryScreen>();
            BuildMinimalScreen(screen);

            Interactor.SetServices(runner, null);
            Interactor.SetGameServices(quests, screen);
        }

        public override void TearDown()
        {
            if (screen != null) screen.Close();
            Time.timeScale = 1f;
            Object.Destroy(services);
            Object.Destroy(player);
            Object.Destroy(ground);
            foreach (var o in spawned) if (o != null) Object.Destroy(o);
            spawned.Clear();
            if (actions != null) Object.Destroy(actions);
            SaveManager.PathOverride = null;
            if (File.Exists(savePath)) File.Delete(savePath);
            base.TearDown();
        }

        T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        EquipmentDefinition Gear(string id, EquipmentSlot slot, float damage, float stamina)
        {
            var e = Track(ScriptableObject.CreateInstance<EquipmentDefinition>());
            e.id = id;
            e.displayName = id;
            e.category = ItemCategory.Equipment;
            e.maxStack = 1;
            e.slot = slot;
            e.damageMultiplier = damage;
            e.maxStaminaBonus = stamina;
            return e;
        }

        /// <summary>The screen's logic needs grids and a slot template, not visuals.</summary>
        void BuildMinimalScreen(InventoryScreen s)
        {
            var root = new GameObject("ScreenRoot", typeof(RectTransform));
            root.transform.SetParent(services.transform, false);
            var packGrid = new GameObject("PackGrid", typeof(RectTransform));
            packGrid.transform.SetParent(root.transform, false);
            var containerGrid = new GameObject("ContainerGrid", typeof(RectTransform));
            containerGrid.transform.SetParent(root.transform, false);
            var template = new GameObject("SlotTemplate", typeof(RectTransform), typeof(Button));
            template.transform.SetParent(services.transform, false);
            var view = template.AddComponent<ItemSlotView>();

            TestRig.SetField(s, "input", Input);
            TestRig.SetField(s, "pack", Pack);
            TestRig.SetField(s, "equipment", Equipment);
            TestRig.SetField(s, "root", root);
            TestRig.SetField(s, "packGrid", (RectTransform)packGrid.transform);
            TestRig.SetField(s, "containerGrid", (RectTransform)containerGrid.transform);
            TestRig.SetField(s, "slotTemplate", view);
        }

        IEnumerator Start()
        {
            player.SetActive(true);
            services.SetActive(true);
            yield return TestRig.WaitSeconds(0.25f);
        }

        IEnumerator Tap(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        IEnumerator FinishDialogue(List<string> lines = null)
        {
            void Record(string speaker, string text) => lines?.Add(text);
            runner.LineStarted += Record;
            for (int i = 0; i < 80 && runner.IsRunning; i++)
            {
                runner.Advance();
                yield return null;
            }
            runner.LineStarted -= Record;
            Assert.IsFalse(runner.IsRunning, "Dialogue never finished.");
        }

        GameObject InFront(string name, float distance = 1.3f)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(0f, 0f, distance);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.8f, 0f);
            trigger.size = new Vector3(1f, 1.6f, 1f);
            return go;
        }

        QuestDefinition Quest(string id, params ObjectiveDefinition[] objectives)
        {
            var q = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            q.id = id;
            q.title = id;
            q.objectives = objectives;
            return q;
        }

        static ObjectiveDefinition Obj(string id, SignalKind kind, string target) =>
            new ObjectiveDefinition { id = id, kind = kind, targetIds = new[] { target }, requiredCount = 1 };

        static int IndexOf(Inventory inv, ItemDefinition item)
        {
            for (int i = 0; i < inv.Stacks.Count; i++)
                if (inv.Stacks[i].Item == item) return i;
            return -1;
        }

        static DialogueData Lines(string speaker, params string[] text)
        {
            var d = ScriptableObject.CreateInstance<DialogueData>();
            d.defaultSpeaker = speaker;
            d.lines = new DialogueData.Line[text.Length];
            for (int i = 0; i < text.Length; i++) d.lines[i] = new DialogueData.Line(string.Empty, text[i]);
            return d;
        }

        // ───────────────────────────── Tests ─────────────────────────────

        [UnityTest]
        public IEnumerator PressingEOnAPickupPutsItInThePackAndHidesIt()
        {
            var go = InFront("Herb");
            var pickup = go.AddComponent<ItemPickup>();
            pickup.Configure("herb_1", herb, 2);
            int received = 0;
            Pack.ItemReceived += (item, n) => received += n;
            yield return Start();

            Assert.AreSame(pickup, Interactor.Focus);
            StringAssert.Contains("Sunmoss", Interactor.Focus.InteractionPrompt);
            yield return Tap(keyboard.eKey);

            Assert.AreEqual(2, Pack.Items.CountOf(herb));
            Assert.AreEqual(2, received);
            Assert.IsTrue(pickup.Collected);
            Assert.IsFalse(go.activeSelf);
        }

        [UnityTest]
        public IEnumerator TabOpensThePackAndPausesTheGame_EscapeClosesItWithoutPausing()
        {
            yield return Start();
            yield return Tap(keyboard.tabKey);
            Assert.IsTrue(screen.IsOpen, "Tab should open the pack.");
            Assert.IsTrue(Input.GameplayBlocked);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(ModalScreens.AnyOpen);

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(screen.IsOpen, "Escape should close the pack...");
            Assert.IsFalse(pause.IsPaused, "...not open the pause menu.");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Input.GameplayBlocked);

            yield return Tap(keyboard.iKey);
            Assert.IsTrue(screen.IsOpen, "I also opens the pack.");
            yield return Tap(keyboard.tabKey);
            Assert.IsFalse(screen.IsOpen, "Tab toggles it closed.");

            yield return Tap(keyboard.escapeKey);
            Assert.IsTrue(pause.IsPaused, "With nothing open, Escape pauses.");
            yield return Tap(keyboard.tabKey);
            Assert.IsFalse(screen.IsOpen, "The pack can't open over the pause menu.");
            pause.Resume();
        }

        [UnityTest]
        public IEnumerator UsingFoodHealsAndEquippingGearChangesStats()
        {
            yield return Start();
            var health = player.GetComponent<HealthComponent>();
            var stamina = player.GetComponent<StaminaComponent>();
            var combat = player.GetComponent<PlayerMeleeCombat>();
            float baseStamina = stamina.Max;
            Assert.AreSame(blade, Equipment.Weapon);
            Assert.AreEqual(1f, combat.DamageMultiplier);

            Pack.Items.Add(herb, 1);
            Pack.Items.Add(sabre);
            Pack.Items.Add(token);
            health.ApplyDamage(new DamageInfo(50f, null, Vector3.zero, Vector3.forward));
            float hurt = health.Current;

            Assert.IsTrue(screen.OpenPack());
            screen.ClickPackSlot(IndexOf(Pack.Items, herb));
            Assert.AreEqual(hurt + 20f, health.Current, 0.01f, "Sunmoss heals 20.");
            Assert.IsFalse(Pack.Items.Contains(herb));

            screen.ClickPackSlot(IndexOf(Pack.Items, sabre));
            Assert.AreSame(sabre, Equipment.Weapon);
            Assert.AreEqual(1.25f, combat.DamageMultiplier, 0.001f);
            Assert.IsTrue(Pack.Items.Contains(blade), "The old weapon goes back in the pack.");
            Assert.IsFalse(Pack.Items.Contains(sabre));

            screen.ClickPackSlot(IndexOf(Pack.Items, token));
            Assert.AreSame(token, Equipment.Charm);
            Assert.AreEqual(baseStamina + 20f, stamina.Max, 0.01f);

            Assert.IsTrue(Equipment.Unequip(EquipmentSlot.Charm));
            Assert.AreEqual(baseStamina, stamina.Max, 0.01f);
            Assert.IsFalse(Equipment.Unequip(EquipmentSlot.Weapon), "Weapons are swapped, not removed.");
            screen.Close();
        }

        [UnityTest]
        public IEnumerator FoodIsNotWastedAtFullHealth()
        {
            yield return Start();
            Pack.Items.Add(herb, 1);
            Assert.IsFalse(Pack.Use(herb));
            Assert.AreEqual(1, Pack.Items.CountOf(herb));
        }

        [UnityTest]
        public IEnumerator OpeningAChestShowsItsContentsAndMovesItemsBothWays()
        {
            var go = InFront("Chest");
            var chest = go.AddComponent<StorageChest>();
            chest.ConfigureStorage("test_chest", "Chest", new ItemAmount(herb, 3));
            yield return Start();

            yield return Tap(keyboard.eKey);
            Assert.IsTrue(screen.IsOpen);
            Assert.AreSame(chest, screen.OpenChest);
            Assert.IsTrue(chest.IsOpen, "Lid opens with the screen.");

            screen.ClickContainerSlot(0);
            Assert.AreEqual(3, Pack.Items.CountOf(herb));
            Assert.AreEqual(0, chest.Contents.CountOf(herb));

            screen.ClickPackSlot(0);
            Assert.AreEqual(3, chest.Contents.CountOf(herb));

            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(screen.IsOpen);
            Assert.IsFalse(chest.IsOpen, "Lid closes with the screen.");
            Assert.IsFalse(pause.IsPaused);
        }

        [UnityTest]
        public IEnumerator TalkingToAVillagerAdvancesTheQuestAndGivesTheGift()
        {
            var quest = Quest("road", Obj("ask", SignalKind.Talked, "ask_about_road"), Obj("look", SignalKind.Examined, "vanished_road"));
            var go = InFront("Mabel", 1.5f);
            var npc = go.AddComponent<NpcCharacter>();
            var first = Track(Lines("Mabel", "Well, look who's back!"));
            var repeat = Track(Lines("Mabel", "Still warm, those rolls."));
            var roadTalk = Track(Lines("Mabel", "There's no road through Mosswood, dear."));
            npc.Configure("Mabel", first, repeat);
            npc.ConfigureQuestContent(new[]
            {
                new NpcCharacter.QuestConversation
                {
                    condition = new QuestCondition(QuestConditionKind.ObjectiveActive, "road", "ask"),
                    dialogue = roadTalk,
                    talkSignal = "ask_about_road",
                },
            }, new ItemAmount(herb, 1));
            yield return Start();
            Assert.IsTrue(quests.StartQuest(quest));

            var heard = new List<string>();
            yield return Tap(keyboard.eKey);
            Assert.IsTrue(runner.IsRunning);
            heard.Add(runner.CurrentText);
            yield return FinishDialogue(heard);

            CollectionAssert.Contains(heard, "Well, look who's back!");
            CollectionAssert.Contains(heard, "There's no road through Mosswood, dear.", "The quest topic follows the greeting.");
            Assert.IsTrue(npc.HasMet);
            Assert.AreEqual(1, Pack.Items.CountOf(herb), "First-meeting gift.");
            Assert.IsTrue(quests.IsObjectiveActive("road", "look"));

            yield return TestRig.WaitSeconds(0.1f);
            yield return Tap(keyboard.eKey);
            Assert.AreEqual("Still warm, those rolls.", runner.CurrentText, "Quest topic no longer applies.");
            yield return FinishDialogue();
        }

        [UnityTest]
        public IEnumerator DefeatingAnEnemyAndRingingTheWaybellCompleteObjectives()
        {
            var quest = Quest("road", Obj("fight", SignalKind.Defeated, "boar_ruffian"), Obj("ring", SignalKind.Activated, "bell"));
            var navGround = Track(TestRig.CreateGround(withNavMesh: true));
            var stats = Track(TestRig.CreateBoarStats());
            stats.id = "boar_ruffian";
            var boar = Track(TestRig.CreateBoar(stats, new Vector3(25f, 0f, 25f), Vector3.forward));
            _ = navGround;

            var go = InFront("Waybell", 1.5f);
            var bell = go.AddComponent<Waybell>();
            bell.Configure("bell", null, null, null, null);
            TestRig.SetField(bell, "chimeSeconds", 0.2f);
            yield return Start();
            boar.SetActive(true);
            quests.StartQuest(quest);
            yield return null;

            boar.GetComponent<HealthComponent>().ApplyDamage(new DamageInfo(9999f, player, boar.transform.position, Vector3.forward));
            Assert.IsTrue(quests.IsObjectiveActive("road", "ring"), "Killing the boar should complete the fight objective.");

            Assert.AreEqual("Ring", Interactor.Focus?.InteractionPrompt);
            yield return Tap(keyboard.eKey);
            Assert.IsTrue(bell.IsRinging);
            Assert.IsTrue(Input.GameplayBlocked, "No moving while the bell rings.");
            yield return TestRig.WaitUntil(() => !bell.IsRinging, 3f);
            Assert.IsTrue(bell.IsAwake);
            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("road"), "Completes after the narration.");
            yield return FinishDialogue();

            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("road"));
            yield return TestRig.WaitSeconds(0.1f);
            Assert.AreEqual("Listen", Interactor.Focus?.InteractionPrompt);
        }

        [UnityTest]
        public IEnumerator RestingSavesAndRestoringBringsTheWorldBack()
        {
            var quest = Quest("road", Obj("ask", SignalKind.Talked, "x"), Obj("look", SignalKind.Examined, "y"));
            quests.Configure(new[] { quest }, new QuestDefinition[0]);
            var chestGo = Track(new GameObject("Chest"));
            chestGo.transform.position = new Vector3(6f, 0f, 0f);
            var chest = chestGo.AddComponent<StorageChest>();
            chest.ConfigureStorage("test_chest", "Chest", new ItemAmount(herb, 4));
            var pickupGo = Track(new GameObject("Token"));
            pickupGo.transform.position = new Vector3(-6f, 0f, 0f);
            var pickup = pickupGo.AddComponent<ItemPickup>();
            pickup.Configure("token_1", token, 1);
            var bed = InFront("Bed").AddComponent<RestSpot>();
            int savedEvents = 0;
            saves.Saved += () => savedEvents++;
            yield return Start();

            // Progress worth saving.
            quests.StartQuest(quest);
            WorldSignals.Raise(SignalKind.Talked, "x");
            pickup.Interact(Interactor);
            Pack.Items.Add(sabre);
            Assert.IsTrue(Equipment.Equip(sabre));
            chest.Contents.TransferAll(herb, Pack.Items);
            player.GetComponent<HealthComponent>().ApplyDamage(new DamageInfo(30f, null, Vector3.zero, Vector3.forward));
            yield return TestRig.WaitSeconds(0.8f); // let the hit stun and knockback finish
            player.GetComponent<ArlenPlayerController>().Teleport(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            yield return TestRig.WaitSeconds(0.2f);
            Assert.AreSame(bed, Interactor.Focus, "Player should be facing the bed.");

            yield return Tap(keyboard.eKey); // rest in the bed (no fader: instant)
            Assert.AreEqual(1, savedEvents, "Resting saves.");
            Assert.IsTrue(File.Exists(savePath));
            yield return FinishDialogue();
            Vector3 savedPosition = player.transform.position;

            // Mess everything up.
            player.GetComponent<ArlenPlayerController>().Teleport(new Vector3(10f, 0.05f, 10f), Quaternion.identity);
            WorldSignals.Raise(SignalKind.Examined, "y");
            Pack.Items.Clear();
            chest.Contents.Clear();
            var freshPickup = Track(new GameObject("TokenAgain")).AddComponent<ItemPickup>();
            freshPickup.Configure("token_1", token, 1);
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("road"));

            Assert.AreEqual(SaveReadResult.Ok, SaveFile.Read(savePath, out var data));
            saves.RestoreInto(data);
            yield return null;

            Assert.Less(Vector3.Distance(savedPosition, player.transform.position), 0.2f, "Player back where they rested.");
            Assert.AreEqual(1f, player.GetComponent<HealthComponent>().Normalized, 0.01f, "Rest healed before saving.");
            Assert.AreEqual(4, Pack.Items.CountOf(herb));
            Assert.IsTrue(Pack.Items.Contains(token));
            Assert.IsTrue(Pack.Items.Contains(blade), "Swapped-out starting blade is in the pack.");
            Assert.AreSame(sabre, Equipment.Weapon);
            Assert.AreEqual(1.25f, player.GetComponent<PlayerMeleeCombat>().DamageMultiplier, 0.001f);
            Assert.AreEqual(0, chest.Contents.CountOf(herb));
            Assert.IsTrue(freshPickup.Collected, "A collected pickup stays collected after loading.");
            Assert.IsTrue(quests.IsObjectiveActive("road", "look"), "Quest progress rolls back to the save.");
        }
    }
}
