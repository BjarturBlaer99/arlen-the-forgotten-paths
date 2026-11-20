using System;
using System.Collections.Generic;
using Arlen.Core;
using Arlen.DialogueSystem;
using Arlen.Items;
using Arlen.Quests;
using UnityEditor;
using UnityEngine;
using static Arlen.Editor.PrototypeSceneBuilder;

namespace Arlen.Editor
{
    /// <summary>
    /// Milestone 4 data: item/equipment assets, the item database, the opening of "The Road That Vanished"
    /// and its quest dialogue. Like the other builders, existing assets are reused untouched so edits made
    /// in the Inspector survive; only missing assets are created. The item database list is always resynced.
    /// </summary>
    public static class RpgContentBuilder
    {
        public const string ItemsFolder = Root + "/Data/Items";
        public const string QuestsFolder = Root + "/Data/Quests";
        public const string DialogueFolder = Root + "/Data/Dialogue";
        public const string DatabasePath = ItemsFolder + "/ItemDatabase.asset";
        public const string RoadQuestPath = QuestsFolder + "/Quest_RoadThatVanished.asset";

        public const string RoadQuestId = "road_that_vanished";
        public const string AskObjective = "ask_about_road";
        public const string WaybellId = "waybell_mosswood";
        public const string ShrineAreaId = "waybell_shrine";
        public const string VanishedRoadId = "vanished_road";
        public const string BoarId = "boar_ruffian";

        internal sealed class Content
        {
            public EquipmentDefinition TravelersBlade, PathwardenSabre, PathwardenToken;
            public ItemDefinition Sunmoss, HoneyRoll, TrailRation, Whetstone, Rope, OldCoins;
            public ItemDatabase Database;
        }

        static Content cached;

        /// <summary>Loads (or creates) all Milestone 4 items once per builder run.</summary>
        internal static Content LoadItems(bool forceReload = false)
        {
            if (cached != null && !forceReload && cached.Database != null) return cached;
            EnsureFolder(ItemsFolder);

            var c = new Content
            {
                TravelersBlade = Equipment("Weapon_TravelersBlade", "travelers_blade", "Traveler's Blade", EquipmentSlot.Weapon,
                    "Your trusty sword. Nicked, reground and nicked again on a hundred roads.",
                    new Color(0.75f, 0.78f, 0.85f), e =>
                    {
                        e.damageMultiplier = 1f;
                        e.bladeColor = new Color(0.82f, 0.85f, 0.9f);
                        e.bladeLengthScale = 1f;
                    }),
                PathwardenSabre = Equipment("Weapon_PathwardenSabre", "pathwarden_sabre", "Pathwarden Sabre", EquipmentSlot.Weapon,
                    "Eldon's old blade, given to you on your sixteenth birthday with strict orders never to need it.",
                    new Color(0.45f, 0.8f, 0.8f), e =>
                    {
                        e.damageMultiplier = 1.25f;
                        e.bladeColor = new Color(0.55f, 0.88f, 0.88f);
                        e.bladeLengthScale = 1.15f;
                    }),
                PathwardenToken = Equipment("Charm_PathwardenToken", "pathwarden_token", "Pathwarden Token", EquipmentSlot.Charm,
                    "A worn brass token stamped with a compass rose. Holding it makes the road ahead feel shorter.",
                    new Color(0.85f, 0.68f, 0.3f), e => e.maxStaminaBonus = 20f),
                Sunmoss = Item("Food_Sunmoss", "sunmoss", "Sunmoss", ItemCategory.Consumable, 10,
                    "Soft golden moss that grows where old roads once ran. Chewing it eases aches.",
                    new Color(0.85f, 0.8f, 0.3f), heal: 20f),
                HoneyRoll = Item("Food_HoneyRoll", "honey_roll", "Honey-Cinnamon Roll", ItemCategory.Consumable, 5,
                    "Still warm. Mabel's finest, and payment in advance for checking on Eldon.",
                    new Color(0.85f, 0.55f, 0.25f), heal: 45f),
                TrailRation = Item("Food_TrailRation", "trail_ration", "Trail Ration", ItemCategory.Consumable, 10,
                    "Dried fruit, nuts and hard cheese wrapped in waxed cloth.",
                    new Color(0.6f, 0.45f, 0.3f), heal: 30f),
                Whetstone = Item("Material_Whetstone", "whetstone", "Whetstone", ItemCategory.Material, 5,
                    "For keeping an edge between visits to Bram.", new Color(0.5f, 0.5f, 0.55f)),
                Rope = Item("Material_Rope", "rope", "Coil of Rope", ItemCategory.Material, 3,
                    "Twenty feet of good hemp. Every adventurer's oldest friend.", new Color(0.75f, 0.65f, 0.45f)),
                OldCoins = Item("Material_OldCoins", "old_coins", "Old Coins", ItemCategory.Material, 99,
                    "Coins from a dozen towns. Bram only accepts about half of them.", new Color(0.95f, 0.8f, 0.35f)),
            };

            c.Database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (c.Database == null)
            {
                c.Database = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(c.Database, DatabasePath);
            }
            SyncDatabase(c.Database);
            AssetDatabase.SaveAssets();
            cached = c;
            return c;
        }

        /// <summary>Lists every ItemDefinition under Data/Items (including ones you add by hand).</summary>
        static void SyncDatabase(ItemDatabase db)
        {
            var all = new List<ItemDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null) all.Add(item);
            }
            all.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            db.SetItems(all);
            EditorUtility.SetDirty(db);
        }

        static ItemDefinition Item(string asset, string id, string displayName, ItemCategory category, int maxStack,
            string description, Color icon, float heal = 0f)
        {
            return LoadOrCreate<ItemDefinition>(asset, item =>
            {
                item.id = id;
                item.displayName = displayName;
                item.category = category;
                item.maxStack = maxStack;
                item.description = description;
                item.iconColor = icon;
                item.healAmount = heal;
            });
        }

        static EquipmentDefinition Equipment(string asset, string id, string displayName, EquipmentSlot slot, string description,
            Color icon, Action<EquipmentDefinition> configure)
        {
            return LoadOrCreate<EquipmentDefinition>(asset, e =>
            {
                e.id = id;
                e.displayName = displayName;
                e.category = ItemCategory.Equipment;
                e.maxStack = 1;
                e.slot = slot;
                e.description = description;
                e.iconColor = icon;
                configure(e);
            });
        }

        static T LoadOrCreate<T>(string assetName, Action<T> init) where T : ScriptableObject
        {
            string path = $"{ItemsFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing; // keep designers' edits
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ───────────────────────────── Quest ─────────────────────────────

        internal static QuestDefinition LoadRoadQuest()
        {
            var existing = AssetDatabase.LoadAssetAtPath<QuestDefinition>(RoadQuestPath);
            if (existing != null) return existing;
            EnsureFolder(QuestsFolder);

            var q = ScriptableObject.CreateInstance<QuestDefinition>();
            q.id = RoadQuestId;
            q.title = "The Road That Vanished";
            q.description = "The Mosswood Road, the road you inked onto Eldon's map yourself, is gone, and nobody in Haven remembers it ever existed. Except you.";
            q.objectives = new[]
            {
                Objective(AskObjective, "Ask Mabel or Bram about the Mosswood Road", SignalKind.Talked, AskObjective),
                Objective("investigate_road", "Find where the road ends in Mosswood", SignalKind.Examined, VanishedRoadId),
                Objective("defeat_ruffian", "Defeat the Boar Ruffian prowling the Mosswood clearing", SignalKind.Defeated, BoarId),
                Objective("find_shrine", "Follow the faint humming deeper into Mosswood", SignalKind.EnteredArea, ShrineAreaId),
                Objective("ring_waybell", "Ring the silent Waybell", SignalKind.Activated, WaybellId),
            };
            q.completionText = "The bell remembers the road. Somewhere beyond the brambles, so might Eldon.";
            AssetDatabase.CreateAsset(q, RoadQuestPath);
            AssetDatabase.SaveAssets();
            return q;
        }

        static ObjectiveDefinition Objective(string id, string description, SignalKind kind, string target) => new ObjectiveDefinition
        {
            id = id,
            description = description,
            kind = kind,
            targetIds = new[] { target },
            requiredCount = 1,
        };

        // ───────────────────────────── Dialogue ─────────────────────────────

        internal static DialogueData MabelRoadDialogue() => LoadOrCreateDialogue("Mabel_Quest_Road", "Mabel",
            ("You", "Mabel, have any traders come down the Mosswood Road lately? From the northern market?"),
            ("", "The Mosswood... road? There's no road through Mosswood, dear. Just trees, boars and that silly old signpost."),
            ("", "Though now you mention it, Eldon asked me something just like that. The last morning I saw him."),
            ("", "He kept staring north, muttering about \"the bells going quiet\". Then he bought two loaves instead of one."),
            ("You", "Two loaves. He was packing for a trip."),
            ("", "If you go poking about in Mosswood, mind those ruffians. And you come back for supper, you hear?"));

        internal static DialogueData BramRoadDialogue() => LoadOrCreateDialogue("Bram_Quest_Road", "Bram",
            ("You", "Bram, you've hauled ore down the Mosswood Road a hundred times. When did it close?"),
            ("", "Road? Kid, the only road north of the stream is the one my boots make."),
            ("", "...Though Eldon came by three days back, wanting a lantern and a good knife. Said he was \"following a line that shouldn't be there\"."),
            ("", "Didn't make sense then either. If you're going after him, there's a boar ruffian camped in the clearing. Watch its shoulders."));

        static DialogueData LoadOrCreateDialogue(string assetName, string speaker, params (string speaker, string text)[] lines)
        {
            string path = $"{DialogueFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (existing != null) return existing;
            EnsureFolder(DialogueFolder);
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.defaultSpeaker = speaker;
            data.lines = new DialogueData.Line[lines.Length];
            for (int i = 0; i < lines.Length; i++) data.lines[i] = new DialogueData.Line(lines[i].speaker, lines[i].text);
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        /// <summary>Quest conversation shared by Mabel and Bram: plays while "ask about the road" is the current objective.</summary>
        internal static NPC.NpcCharacter.QuestConversation RoadTalk(DialogueData dialogue) => new NPC.NpcCharacter.QuestConversation
        {
            condition = new QuestCondition(QuestConditionKind.ObjectiveActive, RoadQuestId, AskObjective),
            dialogue = dialogue,
            talkSignal = AskObjective,
            once = false,
        };
    }
}
