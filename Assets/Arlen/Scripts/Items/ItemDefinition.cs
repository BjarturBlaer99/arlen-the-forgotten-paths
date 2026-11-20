using UnityEngine;

namespace Arlen.Items
{
    public enum ItemCategory
    {
        Consumable,
        Material,
        Keepsake,
        Equipment,
    }

    /// <summary>
    /// An item type (Create ▸ Arlen ▸ Item). Instances in inventories are just (definition, count).
    /// The icon colour stands in for icon art until it exists.
    /// </summary>
    [CreateAssetMenu(menuName = "Arlen/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id used by save files. Never change it once players have saves.")]
        public string id;
        public string displayName = "Item";
        [TextArea(2, 4)] public string description;
        public ItemCategory category = ItemCategory.Material;
        [Min(1)] public int maxStack = 20;
        [Tooltip("Placeholder icon colour.")]
        public Color iconColor = new Color(0.7f, 0.6f, 0.4f);

        [Header("Consumable")]
        [Tooltip("Health restored when used (consumables only).")]
        [Min(0f)] public float healAmount;

        public bool IsStackable => maxStack > 1;
        public virtual bool IsUsable => category == ItemCategory.Consumable && healAmount > 0f;

        /// <summary>One line of stats for the item detail panel.</summary>
        public virtual string StatsText
        {
            get
            {
                return category == ItemCategory.Consumable && healAmount > 0f ? $"Restores {healAmount:0} health" : string.Empty;
            }
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) Debug.LogWarning($"Item '{name}' has no id; it can't be saved.", this);
        }
    }
}
