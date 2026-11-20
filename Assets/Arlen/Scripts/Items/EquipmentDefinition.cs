using System.Text;
using UnityEngine;

namespace Arlen.Items
{
    public enum EquipmentSlot
    {
        Weapon,
        Charm,
    }

    /// <summary>
    /// Wearable/wieldable item (Create ▸ Arlen ▸ Equipment). Stat modifiers are applied by
    /// <see cref="PlayerEquipment"/>; the look fields are read only by presentation components.
    /// </summary>
    [CreateAssetMenu(menuName = "Arlen/Equipment", fileName = "Equipment")]
    public class EquipmentDefinition : ItemDefinition
    {
        [Header("Equipment")]
        public EquipmentSlot slot = EquipmentSlot.Weapon;
        [Tooltip("Multiplies sword damage (weapons and charms stack multiplicatively).")]
        [Min(0.1f)] public float damageMultiplier = 1f;
        public float maxHealthBonus;
        public float maxStaminaBonus;

        [Header("Placeholder Look (weapons)")]
        public Color bladeColor = new Color(0.82f, 0.85f, 0.9f);
        [Tooltip("Blade length relative to the placeholder sword.")]
        [Min(0.2f)] public float bladeLengthScale = 1f;

        public override bool IsUsable => false;

        public override string StatsText
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(slot == EquipmentSlot.Weapon ? "Weapon" : "Charm");
                if (!Mathf.Approximately(damageMultiplier, 1f)) sb.Append($" · Damage ×{damageMultiplier:0.##}");
                if (maxHealthBonus != 0f) sb.Append($" · Max health {maxHealthBonus:+0;-0}");
                if (maxStaminaBonus != 0f) sb.Append($" · Max stamina {maxStaminaBonus:+0;-0}");
                return sb.ToString();
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            category = ItemCategory.Equipment;
            maxStack = 1;
        }
    }
}
