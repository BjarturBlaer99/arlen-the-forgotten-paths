using System;
using Arlen.CameraSystem;
using Arlen.Core;
using Arlen.Interaction;
using Arlen.Items;
using Arlen.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>
    /// The pack screen (Inventory button) and the chest transfer screen (opening a chest). Pauses the game,
    /// frees the cursor and blocks gameplay input while open. Click / gamepad A on a slot performs its
    /// action: use or equip in the pack, move between chest and pack in chest mode.
    /// </summary>
    public class InventoryScreen : MonoBehaviour, IModalScreen
    {
        [Header("Player")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerInventory pack;
        [SerializeField] PlayerEquipment equipment;

        [Header("Layout")]
        [SerializeField] GameObject root;
        [SerializeField] Text title;
        [SerializeField] GameObject equipmentSection;
        [SerializeField] GameObject containerSection;
        [SerializeField] Text containerTitle;
        [SerializeField] RectTransform packGrid;
        [SerializeField] RectTransform containerGrid;
        [Tooltip("Inactive slot cloned to fill the grids.")]
        [SerializeField] ItemSlotView slotTemplate;
        [SerializeField] ItemSlotView weaponSlot;
        [SerializeField] ItemSlotView charmSlot;
        [SerializeField] Text weaponLabel;
        [SerializeField] Text charmLabel;

        [Header("Details")]
        [SerializeField] Text detailName;
        [SerializeField] Text detailBody;
        [SerializeField] Text detailStats;
        [SerializeField] Text hint;
        [SerializeField] Text status;

        ItemSlotView[] packSlots = Array.Empty<ItemSlotView>();
        ItemSlotView[] containerSlots = Array.Empty<ItemSlotView>();
        Action onClosed;
        float previousTimeScale = 1f;
        ItemSlotView focused;

        public bool IsOpen { get; private set; }
        public StorageChest OpenChest { get; private set; }
        public string StatusText => status != null ? status.text : string.Empty;
        public event Action<bool> OpenChanged;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
            Hook(weaponSlot, OnWeaponSlotClicked);
            Hook(charmSlot, OnCharmSlotClicked);
        }

        void OnEnable()
        {
            if (input != null) input.InventoryPressed += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.InventoryPressed -= Toggle;
            Close();
        }

        void Hook(ItemSlotView slot, Action<ItemSlotView> clicked)
        {
            if (slot == null) return;
            slot.Clicked += clicked;
            slot.Focused += ShowDetails;
            slot.Cancelled += Close;
        }

        ItemSlotView[] BuildSlots(RectTransform grid, int count, Action<ItemSlotView> clicked)
        {
            var slots = new ItemSlotView[grid != null && slotTemplate != null ? count : 0];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = Instantiate(slotTemplate, grid);
                slot.name = $"Slot_{i:00}";
                slot.gameObject.SetActive(true);
                Hook(slot, clicked);
                slots[i] = slot;
            }
            return slots;
        }

        void Toggle()
        {
            if (IsOpen) Close();
            else OpenPack();
        }

        public bool OpenPack() => Open(null, null);

        public bool OpenContainer(StorageChest chest, Action closed) => chest != null && Open(chest, closed);

        bool Open(StorageChest chest, Action closed)
        {
            if (IsOpen || pack == null || (input != null && input.GameplayBlocked)) return false;

            if (packSlots.Length == 0) packSlots = BuildSlots(packGrid, pack.Items.Capacity, OnPackSlotClicked);
            if (chest != null && containerSlots.Length < chest.Contents.Capacity)
            {
                foreach (var s in containerSlots) Destroy(s.gameObject);
                containerSlots = BuildSlots(containerGrid, chest.Contents.Capacity, OnContainerSlotClicked);
            }

            IsOpen = true;
            OpenChest = chest;
            onClosed = closed;
            pack.Items.Changed += Refresh;
            if (chest != null) chest.Contents.Changed += Refresh;
            if (equipment != null) equipment.Changed += Refresh;

            if (root != null) root.SetActive(true);
            if (equipmentSection != null) equipmentSection.SetActive(chest == null);
            if (containerSection != null) containerSection.SetActive(chest != null);
            if (title != null) title.text = chest != null ? $"{chest.DisplayName}  ·  Pack" : "Pack";
            if (containerTitle != null) containerTitle.text = chest != null ? chest.DisplayName : string.Empty;
            if (hint != null)
                hint.text = chest != null
                    ? "Click / (A): move between chest and pack      Esc / Tab: close"
                    : "Click / (A): use or equip      Esc / Tab: close";
            SetStatus(string.Empty);

            if (input != null) input.PushBlock();
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ThirdPersonCameraController.SetCursorLocked(false);
            ModalScreens.Register(this);

            Refresh();
            var first = chest != null && containerSlots.Length > 0 ? containerSlots[0] : packSlots.Length > 0 ? packSlots[0] : null;
            Select(first);
            ShowDetails(first);
            OpenChanged?.Invoke(true);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ModalScreens.Unregister(this);

            pack.Items.Changed -= Refresh;
            if (OpenChest != null) OpenChest.Contents.Changed -= Refresh;
            if (equipment != null) equipment.Changed -= Refresh;
            OpenChest = null;

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (root != null) root.SetActive(false);
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            ThirdPersonCameraController.SetCursorLocked(true);
            if (input != null) input.PopBlock();

            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
            OpenChanged?.Invoke(false);
        }

        void Refresh()
        {
            Fill(packSlots, pack.Items);
            if (OpenChest != null) Fill(containerSlots, OpenChest.Contents);
            if (equipment != null)
            {
                if (weaponSlot != null) weaponSlot.Set(equipment.Weapon, 1);
                if (charmSlot != null) charmSlot.Set(equipment.Charm, 1);
                if (weaponLabel != null) weaponLabel.text = equipment.Weapon != null ? equipment.Weapon.displayName : "No weapon";
                if (charmLabel != null) charmLabel.text = equipment.Charm != null ? equipment.Charm.displayName : "No charm";
            }
            if (focused != null) ShowDetails(focused);
        }

        static void Fill(ItemSlotView[] slots, Inventory inventory)
        {
            var stacks = inventory.Stacks;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < stacks.Count) slots[i].Set(stacks[i].Item, stacks[i].Count);
                else slots[i].Set(null, 0);
            }
        }

        // ───────────── Actions ─────────────

        void OnPackSlotClicked(ItemSlotView slot)
        {
            var item = slot.Item;
            if (item == null) return;

            if (OpenChest != null)
            {
                if (pack.Items.TransferAll(item, OpenChest.Contents) == 0) SetStatus($"The {OpenChest.DisplayName.ToLowerInvariant()} is full.");
                else SetStatus(string.Empty);
                return;
            }

            if (item is EquipmentDefinition gear)
            {
                if (equipment != null && equipment.Equip(gear)) SetStatus($"Equipped {gear.displayName}.");
            }
            else if (item.IsUsable)
            {
                SetStatus(pack.Use(item) ? $"Used {item.displayName}." : "You're already in good health.");
            }
            else
            {
                SetStatus(item.category == ItemCategory.Keepsake ? "Something to keep, not to use." : "Not something you can use right now.");
            }
        }

        void OnContainerSlotClicked(ItemSlotView slot)
        {
            if (slot.Item == null || OpenChest == null) return;
            SetStatus(OpenChest.Contents.TransferAll(slot.Item, pack.Items) == 0 ? "Your pack is full." : string.Empty);
        }

        void OnWeaponSlotClicked(ItemSlotView slot) =>
            SetStatus("Equip another weapon from your pack to swap it.");

        void OnCharmSlotClicked(ItemSlotView slot)
        {
            if (equipment == null || equipment.Charm == null) return;
            string name = equipment.Charm.displayName;
            SetStatus(equipment.Unequip(EquipmentSlot.Charm) ? $"Put {name} back in your pack." : "Your pack is full.");
        }

        // ───────────── Details ─────────────

        void ShowDetails(ItemSlotView slot)
        {
            focused = slot;
            var item = slot != null ? slot.Item : null;
            if (detailName != null) detailName.text = item != null ? item.displayName : string.Empty;
            if (detailBody != null) detailBody.text = item != null ? item.description : string.Empty;
            if (detailStats != null)
            {
                string stats = item != null ? item.StatsText : string.Empty;
                if (item is EquipmentDefinition gear && equipment != null && equipment.Get(gear.slot) == gear && slot != weaponSlot && slot != charmSlot)
                    stats += "  (equipped)";
                detailStats.text = stats;
            }
        }

        void SetStatus(string message)
        {
            if (status != null) status.text = message;
        }

        static void Select(ItemSlotView slot)
        {
            if (slot != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slot.gameObject);
        }

        // Test hooks: drive the screen without simulating pointer events.
        public void ClickPackSlot(int index) { if (index < packSlots.Length) OnPackSlotClicked(packSlots[index]); }
        public void ClickContainerSlot(int index) { if (index < containerSlots.Length) OnContainerSlotClicked(containerSlots[index]); }
    }
}
