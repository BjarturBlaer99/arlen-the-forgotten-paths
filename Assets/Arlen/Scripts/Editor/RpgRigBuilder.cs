using Arlen.Interaction;
using Arlen.Items;
using Arlen.Player;
using Arlen.Quests;
using Arlen.Saving;
using Arlen.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Arlen.Editor.PrototypeSceneBuilder;

namespace Arlen.Editor
{
    /// <summary>
    /// Milestone 4 additions to the shared player rig: inventory/equipment/save components on the player
    /// prefab, quest and save services, and the HUD pieces (objective tracker, toasts, pack/chest screen,
    /// pause-menu save buttons). Called from <see cref="PrototypeSceneBuilder"/> so every scene gets them.
    /// </summary>
    internal static class RpgRigBuilder
    {
        static readonly Color PanelColor = new Color(0.07f, 0.065f, 0.09f, 0.96f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.45f);
        static readonly Color Teal = new Color(0.6f, 0.95f, 0.9f);
        static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);

        /// <summary>Idempotent: safe on new and existing (possibly user-edited) player prefabs.</summary>
        internal static bool AddMilestone4PlayerComponents(GameObject root)
        {
            bool changed = false;
            var content = RpgContentBuilder.LoadItems();
            if (root.GetComponent<PlayerInventory>() == null) { root.AddComponent<PlayerInventory>(); changed = true; }
            var equipment = root.GetComponent<PlayerEquipment>();
            if (equipment == null)
            {
                equipment = root.AddComponent<PlayerEquipment>();
                Set(equipment, "startingWeapon", content.TravelersBlade);
                changed = true;
            }
            if (root.GetComponent<PlayerSaveState>() == null) { root.AddComponent<PlayerSaveState>(); changed = true; }

            var visual = root.transform.Find("Visual");
            var blade = root.transform.Find("Visual/Body/SwordPivot/Blade");
            if (visual != null && visual.GetComponent<EquipmentVisuals>() == null)
            {
                var looks = visual.gameObject.AddComponent<EquipmentVisuals>();
                Set(looks, "equipment", equipment);
                if (blade != null)
                {
                    Set(looks, "blade", blade);
                    Set(looks, "bladeRenderer", blade.GetComponent<Renderer>());
                }
                else Debug.LogWarning("[Arlen] Player prefab has no Visual/Body/SwordPivot/Blade; equipped weapons won't change the sword's look.");
                changed = true;
            }
            return changed;
        }

        internal static void BuildRpgSystems(Transform canvas, Font font, GameObject player, GameObject systems, HudRefs refs, GameObject pauseRoot)
        {
            var content = RpgContentBuilder.LoadItems();
            var quests = systems.AddComponent<QuestManager>();
            var saves = systems.AddComponent<SaveManager>();
            Set(saves, "items", content.Database);

            var tracker = BuildQuestTracker(canvas, font, quests);
            var toasts = BuildToasts(canvas, font);
            var screen = BuildInventoryScreen(canvas, font, player);

            var notifications = canvas.gameObject.AddComponent<HudNotifications>();
            notifications.Configure(toasts, quests, player.GetComponent<PlayerInventory>(), saves);
            _ = tracker;

            var interactor = player.GetComponent<PlayerInteractor>();
            if (interactor != null)
            {
                Set(interactor, "quests", quests);
                Set(interactor, "inventoryScreen", screen);
            }

            BuildPauseSaveButtons(pauseRoot, font, saves);

            refs.Quests = quests;
            refs.Saves = saves;
            refs.Inventory = screen;
        }

        // ───────────────────────────── HUD ─────────────────────────────

        static QuestTrackerUI BuildQuestTracker(Transform canvas, Font font, QuestManager quests)
        {
            var root = Panel(canvas, "QuestTracker", new Color(0.05f, 0.05f, 0.08f, 0.45f));
            Anchor((RectTransform)root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(620f, 110f));
            var title = MakeText(root.transform, "QuestTitle", font, 26, TextAnchor.UpperLeft, "Quest");
            title.color = Gold;
            title.fontStyle = FontStyle.Bold;
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(584f, 36f));
            var objective = MakeText(root.transform, "Objective", font, 24, TextAnchor.UpperLeft, "• Objective");
            objective.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(objective.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(584f, 56f));

            var tracker = canvas.gameObject.AddComponent<QuestTrackerUI>();
            tracker.Configure(quests);
            Set(tracker, "root", root);
            Set(tracker, "title", title);
            Set(tracker, "objective", objective);
            root.SetActive(false);
            return tracker;
        }

        static ToastFeed BuildToasts(Transform canvas, Font font)
        {
            var area = new GameObject("Toasts", typeof(RectTransform), typeof(VerticalLayoutGroup));
            area.transform.SetParent(canvas, false);
            Anchor((RectTransform)area.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -168f), new Vector2(620f, 200f));
            var layout = area.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;

            var template = MakeText(area.transform, "ToastTemplate", font, 26, TextAnchor.MiddleRight, "Toast");
            template.gameObject.AddComponent<LayoutElement>().minHeight = 34f;
            template.raycastTarget = false;
            template.gameObject.SetActive(false);

            var feed = area.AddComponent<ToastFeed>();
            Set(feed, "template", template);
            return feed;
        }

        // ───────────────────────────── Pack / chest screen ─────────────────────────────

        static InventoryScreen BuildInventoryScreen(Transform canvas, Font font, GameObject player)
        {
            var root = Panel(canvas, "InventoryScreen", new Color(0f, 0f, 0f, 0.55f));
            Stretch((RectTransform)root.transform);

            var panel = Panel(root.transform, "Panel", PanelColor);
            Anchor((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1360f, 840f));
            var p = panel.transform;

            var title = MakeText(p, "Title", font, 40, TextAnchor.MiddleCenter, "Pack");
            title.color = Gold;
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1200f, 56f));

            // Left: equipment (pack mode) or chest contents (chest mode).
            var equipmentSection = Section(p, "EquipmentSection", new Vector2(40f, -100f));
            Label(equipmentSection.transform, font, "Equipped", Vector2.zero, 28, Color.white);
            var weaponSlot = MakeSlot(equipmentSection.transform, "WeaponSlot", font, new Vector2(0f, -56f), 110f);
            Label(equipmentSection.transform, font, "Weapon", new Vector2(130f, -62f), 22, Muted);
            var weaponLabel = Label(equipmentSection.transform, font, "Weapon", new Vector2(130f, -94f), 30, Color.white);
            var charmSlot = MakeSlot(equipmentSection.transform, "CharmSlot", font, new Vector2(0f, -196f), 110f);
            Label(equipmentSection.transform, font, "Charm", new Vector2(130f, -202f), 22, Muted);
            var charmLabel = Label(equipmentSection.transform, font, "No charm", new Vector2(130f, -234f), 30, Color.white);
            Label(equipmentSection.transform, font, "Click an item in your pack to use or equip it.\nClick the charm to take it off.", new Vector2(0f, -340f), 22, Muted);

            var containerSection = Section(p, "ContainerSection", new Vector2(40f, -100f));
            var containerTitle = Label(containerSection.transform, font, "Chest", Vector2.zero, 28, Color.white);
            var containerGrid = Grid(containerSection.transform, "ContainerGrid", new Vector2(0f, -56f));

            // Right: the pack.
            var packSection = Section(p, "PackSection", new Vector2(720f, -100f));
            Label(packSection.transform, font, "Pack", Vector2.zero, 28, Color.white);
            var packGrid = Grid(packSection.transform, "PackGrid", new Vector2(0f, -56f));

            // Bottom: item details, status and controls hint.
            var details = Panel(p, "Details", new Color(0f, 0f, 0f, 0.35f));
            Anchor((RectTransform)details.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(1280f, 240f));
            var d = details.transform;
            var detailName = MakeText(d, "Name", font, 32, TextAnchor.UpperLeft, "");
            detailName.color = Gold;
            Anchor(detailName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(900f, 40f));
            var detailStats = MakeText(d, "Stats", font, 22, TextAnchor.UpperLeft, "");
            detailStats.color = Teal;
            Anchor(detailStats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -56f), new Vector2(1200f, 30f));
            var detailBody = MakeText(d, "Description", font, 24, TextAnchor.UpperLeft, "");
            detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(1232f, 80f));
            var status = MakeText(d, "Status", font, 22, TextAnchor.LowerRight, "");
            status.color = new Color(1f, 0.75f, 0.55f);
            Anchor(status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(620f, 30f));
            var hint = MakeText(d, "Hint", font, 20, TextAnchor.LowerLeft, "");
            hint.color = Muted;
            Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 14f), new Vector2(640f, 28f));

            var template = MakeSlot(p, "SlotTemplate", font, Vector2.zero, 88f);
            template.gameObject.SetActive(false);

            var screen = canvas.gameObject.AddComponent<InventoryScreen>();
            Set(screen, "input", player.GetComponent<PlayerInputReader>());
            Set(screen, "pack", player.GetComponent<PlayerInventory>());
            Set(screen, "equipment", player.GetComponent<PlayerEquipment>());
            Set(screen, "root", root);
            Set(screen, "title", title);
            Set(screen, "equipmentSection", equipmentSection);
            Set(screen, "containerSection", containerSection);
            Set(screen, "containerTitle", containerTitle);
            Set(screen, "packGrid", packGrid);
            Set(screen, "containerGrid", containerGrid);
            Set(screen, "slotTemplate", template);
            Set(screen, "weaponSlot", weaponSlot);
            Set(screen, "charmSlot", charmSlot);
            Set(screen, "weaponLabel", weaponLabel);
            Set(screen, "charmLabel", charmLabel);
            Set(screen, "detailName", detailName);
            Set(screen, "detailBody", detailBody);
            Set(screen, "detailStats", detailStats);
            Set(screen, "hint", hint);
            Set(screen, "status", status);
            containerSection.SetActive(false);
            root.SetActive(false);
            return screen;
        }

        static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject Section(Transform parent, string name, Vector2 topLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(600f, 440f));
            return go;
        }

        static Text Label(Transform parent, Font font, string text, Vector2 topLeft, int size, Color color)
        {
            var t = MakeText(parent, "Label", font, size, TextAnchor.UpperLeft, text);
            t.color = color;
            t.raycastTarget = false;
            Anchor(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(580f, size + 12f));
            return t;
        }

        static RectTransform Grid(Transform parent, string name, Vector2 topLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(568f, 376f));
            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(88f, 88f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            return rt;
        }

        /// <summary>Slot button: frame (selection colour) + inset icon + initials + stack count.</summary>
        static ItemSlotView MakeSlot(Transform parent, string name, Font font, Vector2 topLeft, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(size, size));
            var frame = go.GetComponent<Image>();
            frame.color = Color.white;
            var button = go.GetComponent<Button>();
            button.targetGraphic = frame;
            var colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.22f, 0.27f);
            colors.highlightedColor = new Color(0.45f, 0.45f, 0.55f);
            colors.selectedColor = new Color(0.85f, 0.7f, 0.3f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            Stretch((RectTransform)iconGo.transform, size * 0.09f);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;

            var initials = MakeText(go.transform, "Initials", font, Mathf.RoundToInt(size * 0.32f), TextAnchor.MiddleCenter, "");
            initials.raycastTarget = false;
            Stretch(initials.rectTransform);
            var count = MakeText(go.transform, "Count", font, Mathf.RoundToInt(size * 0.24f), TextAnchor.LowerRight, "");
            count.raycastTarget = false;
            Stretch(count.rectTransform, size * 0.1f);

            var view = go.AddComponent<ItemSlotView>();
            Set(view, "button", button);
            Set(view, "icon", icon);
            Set(view, "initials", initials);
            Set(view, "count", count);
            return view;
        }

        // ───────────────────────────── Pause menu ─────────────────────────────

        static void BuildPauseSaveButtons(GameObject pauseRoot, Font font, SaveManager saves)
        {
            var actions = pauseRoot.AddComponent<SaveMenuButtons>();
            var load = MakeButton(pauseRoot.transform, "LoadButton", font, "Load Last Rest", new Vector2(0f, -90f));
            UnityEventTools.AddPersistentListener(load.onClick, actions.LoadLastSave);
            var newGame = MakeButton(pauseRoot.transform, "NewGameButton", font, "New Game", new Vector2(0f, -170f));
            UnityEventTools.AddPersistentListener(newGame.onClick, actions.AskNewGame);

            var confirm = Panel(pauseRoot.transform, "ConfirmNewGame", new Color(0.02f, 0.02f, 0.05f, 0.85f));
            Stretch((RectTransform)confirm.transform);
            var question = MakeText(confirm.transform, "Question", font, 44, TextAnchor.MiddleCenter,
                "Start a new game?\n<size=26>Your saved progress will be erased.</size>");
            question.supportRichText = true;
            Anchor(question.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1000f, 160f));
            var yes = MakeButton(confirm.transform, "ConfirmButton", font, "Start Over", new Vector2(-180f, -40f));
            UnityEventTools.AddPersistentListener(yes.onClick, actions.ConfirmNewGame);
            var no = MakeButton(confirm.transform, "CancelButton", font, "Cancel", new Vector2(180f, -40f));
            UnityEventTools.AddPersistentListener(no.onClick, actions.CancelNewGame);
            confirm.SetActive(false);

            Set(actions, "saves", saves);
            Set(actions, "loadButton", load);
            Set(actions, "confirmNewGamePanel", confirm);
            Set(actions, "cancelNewGameButton", no);
        }
    }
}
