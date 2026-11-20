using System;
using Arlen.Combat;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.Items
{
    /// <summary>The player's pack: an <see cref="Inventory"/> plus using consumables. UI listens to its events.</summary>
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour, ISaveable
    {
        [SerializeField, Min(1)] int capacity = 24;
        [Tooltip("Contents on a new game.")]
        [SerializeField] ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        Inventory items;

        /// <summary>Lazily created so other components can use it regardless of Awake order.</summary>
        public Inventory Items
        {
            get
            {
                if (items == null) Initialize();
                return items;
            }
        }

        /// <summary>(item, count) for every gain that came from the world (pickups, gifts) — not loads or transfers.</summary>
        public event Action<ItemDefinition, int> ItemReceived;
        /// <summary>(item) after a consumable was used.</summary>
        public event Action<ItemDefinition> ItemUsed;

        void Awake()
        {
            if (items == null) Initialize();
        }

        void Initialize()
        {
            items = new Inventory(capacity);
            foreach (var a in startingItems)
                if (a.item != null) items.Add(a.item, Mathf.Max(1, a.count));
        }

        /// <summary>Adds items found in the world and announces them. Returns the number that fit.</summary>
        public int Receive(ItemDefinition item, int count = 1)
        {
            int added = Items.Add(item, count);
            if (added > 0) ItemReceived?.Invoke(item, added);
            return added;
        }

        public bool CanUse(ItemDefinition item) => item != null && item.IsUsable && Items.Contains(item);

        /// <summary>Consumes one of the item and applies its effect. Returns false if it can't be used now.</summary>
        public bool Use(ItemDefinition item)
        {
            if (!CanUse(item)) return false;
            var health = GetComponent<HealthComponent>();
            // Don't waste food at full health (or while defeated).
            if (health == null || !health.IsAlive || health.Current >= health.Max) return false;

            health.Heal(item.healAmount);
            Items.Remove(item, 1);
            ItemUsed?.Invoke(item);
            return true;
        }

        public void CaptureState(SaveData data) => data.inventory = Items.Capture();

        public void RestoreState(SaveData data, SaveContext context) => Items.Restore(data.inventory, context.ResolveItem);
    }
}
