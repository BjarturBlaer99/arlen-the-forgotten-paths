using System;
using System.Collections.Generic;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.Items
{
    /// <summary>A stack of one item type inside an <see cref="Inventory"/>.</summary>
    public sealed class ItemStack
    {
        public ItemDefinition Item { get; }
        public int Count { get; internal set; }

        internal ItemStack(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    /// <summary>Authoring form of "n of an item" (starting contents, pickups, gifts).</summary>
    [Serializable]
    public struct ItemAmount
    {
        public ItemDefinition item;
        [Min(1)] public int count;

        public ItemAmount(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }

    /// <summary>
    /// Engine-independent container with a fixed number of slots. Stackable items fill existing stacks
    /// before opening new slots. Used by the player's pack and by storage chests.
    /// </summary>
    public sealed class Inventory
    {
        readonly List<ItemStack> stacks = new List<ItemStack>();

        public int Capacity { get; }
        public IReadOnlyList<ItemStack> Stacks => stacks;
        public int SlotsUsed => stacks.Count;
        public bool IsFull => stacks.Count >= Capacity;

        /// <summary>Raised after any change to the contents.</summary>
        public event Action Changed;

        public Inventory(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            Capacity = capacity;
        }

        public int CountOf(ItemDefinition item)
        {
            int total = 0;
            foreach (var s in stacks)
                if (s.Item == item) total += s.Count;
            return total;
        }

        public bool Contains(ItemDefinition item) => CountOf(item) > 0;

        /// <summary>How many of the item would fit right now.</summary>
        public int SpaceFor(ItemDefinition item)
        {
            if (item == null) return 0;
            int max = Mathf.Max(1, item.maxStack);
            int space = (Capacity - stacks.Count) * max;
            foreach (var s in stacks)
                if (s.Item == item) space += max - s.Count;
            return space;
        }

        /// <summary>Adds as many as fit. Returns the number actually added.</summary>
        public int Add(ItemDefinition item, int count = 1)
        {
            int added = AddSilently(item, count);
            if (added > 0) Changed?.Invoke();
            return added;
        }

        int AddSilently(ItemDefinition item, int count)
        {
            if (item == null || count <= 0) return 0;
            int max = Mathf.Max(1, item.maxStack);
            int remaining = count;

            for (int i = 0; i < stacks.Count && remaining > 0; i++)
            {
                var s = stacks[i];
                if (s.Item != item || s.Count >= max) continue;
                int take = Mathf.Min(max - s.Count, remaining);
                s.Count += take;
                remaining -= take;
            }
            while (remaining > 0 && stacks.Count < Capacity)
            {
                int take = Mathf.Min(max, remaining);
                stacks.Add(new ItemStack(item, take));
                remaining -= take;
            }
            return count - remaining;
        }

        /// <summary>Removes up to <paramref name="count"/>, taking from the last stacks first. Returns the number removed.</summary>
        public int Remove(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return 0;
            int remaining = count;
            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var s = stacks[i];
                if (s.Item != item) continue;
                int take = Mathf.Min(s.Count, remaining);
                s.Count -= take;
                remaining -= take;
                if (s.Count == 0) stacks.RemoveAt(i);
            }
            int removed = count - remaining;
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        /// <summary>Moves as much of a whole item type as fits into another inventory. Returns the number moved.</summary>
        public int TransferAll(ItemDefinition item, Inventory destination)
        {
            if (item == null || destination == null || destination == this) return 0;
            int moving = Mathf.Min(CountOf(item), destination.SpaceFor(item));
            if (moving <= 0) return 0;
            destination.Add(item, moving);
            Remove(item, moving);
            return moving;
        }

        public void Clear()
        {
            if (stacks.Count == 0) return;
            stacks.Clear();
            Changed?.Invoke();
        }

        // ───────────── Saving ─────────────

        public List<ItemSaveEntry> Capture()
        {
            var list = new List<ItemSaveEntry>(stacks.Count);
            foreach (var s in stacks)
                if (s.Item != null && !string.IsNullOrEmpty(s.Item.id)) list.Add(new ItemSaveEntry(s.Item.id, s.Count));
            return list;
        }

        /// <summary>Replaces the contents. Unknown item ids (removed content) are skipped.</summary>
        public void Restore(IEnumerable<ItemSaveEntry> entries, Func<string, ItemDefinition> resolve)
        {
            stacks.Clear();
            if (entries != null && resolve != null)
                foreach (var e in entries)
                    AddSilently(resolve(e.itemId), e.count);
            Changed?.Invoke();
        }
    }
}
