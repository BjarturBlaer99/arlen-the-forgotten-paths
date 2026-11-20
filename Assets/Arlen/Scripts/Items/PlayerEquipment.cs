using System;
using Arlen.Combat;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.Items
{
    /// <summary>
    /// Weapon and charm slots. Equipping moves the item out of the pack (and the old one back in), then
    /// recomputes damage, max health and max stamina from the base values. Presentation listens to
    /// <see cref="Changed"/>; this component never touches meshes.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEquipment : MonoBehaviour, ISaveable
    {
        [SerializeField] EquipmentDefinition startingWeapon;
        [SerializeField] EquipmentDefinition startingCharm;

        PlayerInventory inventory;
        PlayerMeleeCombat combat;
        HealthComponent health;
        StaminaComponent stamina;
        float baseMaxHealth, baseMaxStamina;
        bool initialized;

        public EquipmentDefinition Weapon { get; private set; }
        public EquipmentDefinition Charm { get; private set; }

        /// <summary>Raised whenever a slot changes (also after loading).</summary>
        public event Action Changed;

        public EquipmentDefinition Get(EquipmentSlot slot) => slot == EquipmentSlot.Weapon ? Weapon : Charm;

        public void SetStartingGear(EquipmentDefinition weapon, EquipmentDefinition charm)
        {
            startingWeapon = weapon;
            startingCharm = charm;
        }

        void Awake() => EnsureInitialized();

        void Start() => Changed?.Invoke(); // let visuals that subscribed in OnEnable/Start catch up

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            inventory = GetComponent<PlayerInventory>();
            combat = GetComponent<PlayerMeleeCombat>();
            health = GetComponent<HealthComponent>();
            stamina = GetComponent<StaminaComponent>();
            // HealthComponent/StaminaComponent create their pools lazily, so Max is valid whatever the Awake order.
            baseMaxHealth = health != null ? health.Max : 0f;
            baseMaxStamina = stamina != null ? stamina.Max : 0f;
            Weapon = startingWeapon;
            Charm = startingCharm;
            Apply();
        }

        /// <summary>Equips an item from the pack. Returns false if it isn't in the pack.</summary>
        public bool Equip(EquipmentDefinition item)
        {
            EnsureInitialized();
            if (item == null || inventory == null || !inventory.Items.Contains(item)) return false;
            var previous = Get(item.slot);
            inventory.Items.Remove(item, 1);
            if (previous != null) inventory.Items.Add(previous, 1); // equipment doesn't stack, so the freed slot fits it
            Set(item.slot, item);
            return true;
        }

        /// <summary>Moves the item in a slot back to the pack. Weapons can only be swapped, not removed.</summary>
        public bool Unequip(EquipmentSlot slot)
        {
            EnsureInitialized();
            var current = Get(slot);
            if (current == null || slot == EquipmentSlot.Weapon || inventory == null) return false;
            if (inventory.Items.Add(current, 1) == 0) return false; // pack full
            Set(slot, null);
            return true;
        }

        void Set(EquipmentSlot slot, EquipmentDefinition item)
        {
            if (slot == EquipmentSlot.Weapon) Weapon = item;
            else Charm = item;
            Apply();
            Changed?.Invoke();
        }

        void Apply()
        {
            float damage = 1f, healthBonus = 0f, staminaBonus = 0f;
            Accumulate(Weapon, ref damage, ref healthBonus, ref staminaBonus);
            Accumulate(Charm, ref damage, ref healthBonus, ref staminaBonus);

            if (combat != null) combat.DamageMultiplier = damage;
            if (health != null)
            {
                float max = Mathf.Max(1f, baseMaxHealth + healthBonus);
                if (!Mathf.Approximately(max, health.Max)) health.SetMaxHealth(max, keepRatio: true);
            }
            if (stamina != null)
            {
                float max = Mathf.Max(1f, baseMaxStamina + staminaBonus);
                if (!Mathf.Approximately(max, stamina.Max)) stamina.SetMaxStamina(max);
            }
        }

        static void Accumulate(EquipmentDefinition item, ref float damage, ref float healthBonus, ref float staminaBonus)
        {
            if (item == null) return;
            damage *= item.damageMultiplier;
            healthBonus += item.maxHealthBonus;
            staminaBonus += item.maxStaminaBonus;
        }

        public void CaptureState(SaveData data)
        {
            data.equipment.hasData = true;
            data.equipment.weaponId = Weapon != null ? Weapon.id : null;
            data.equipment.charmId = Charm != null ? Charm.id : null;
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            EnsureInitialized();
            if (data.equipment == null || !data.equipment.hasData) return;
            Weapon = context.ResolveItem(data.equipment.weaponId) as EquipmentDefinition ?? startingWeapon;
            Charm = context.ResolveItem(data.equipment.charmId) as EquipmentDefinition;
            Apply();
            Changed?.Invoke();
        }
    }
}
