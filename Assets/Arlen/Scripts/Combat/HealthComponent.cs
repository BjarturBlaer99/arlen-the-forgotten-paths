using System;
using Arlen.Core;
using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>
    /// Reusable health for players, enemies and destructibles. Gameplay and UI subscribe to its events;
    /// it knows nothing about visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;

        HealthPool pool;
        int invulnerabilityCount;

        public float Max => Pool.Max;
        public float Current => Pool.Current;
        public float Normalized => Pool.Normalized;
        public bool IsAlive => !Pool.IsDead;
        public bool IsInvulnerable => invulnerabilityCount > 0;

        /// <summary>(current, max)</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>Raised after damage was accepted. Amount in the info is the requested amount.</summary>
        public event Action<DamageInfo> Damaged;
        /// <summary>Raised when an incoming hit was ignored because of invulnerability (e.g. dodge i-frames).</summary>
        public event Action<DamageInfo> DamageAvoided;
        public event Action<float> Healed;
        public event Action Died;
        public event Action Revived;

        HealthPool Pool
        {
            get
            {
                if (pool == null) CreatePool();
                return pool;
            }
        }

        void Awake()
        {
            if (pool == null) CreatePool();
        }

        void CreatePool()
        {
            pool = new HealthPool(maxHealth);
            pool.Changed += (c, m) => HealthChanged?.Invoke(c, m);
            pool.Healed += a => Healed?.Invoke(a);
            pool.Died += () => Died?.Invoke();
            pool.Revived += () => Revived?.Invoke();
        }

        public bool ApplyDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return false;
            if (IsInvulnerable)
            {
                DamageAvoided?.Invoke(info);
                return false;
            }

            // Damaged fires before Changed/Died so listeners can react to the hit itself first.
            Damaged?.Invoke(info);
            Pool.TakeDamage(info.Amount);
            return true;
        }

        public float Heal(float amount) => Pool.Heal(amount);

        public void Revive(float fraction = 1f) => Pool.Revive(fraction);

        public void SetMaxHealth(float max, bool keepRatio = true)
        {
            maxHealth = max;
            Pool.SetMax(max, keepRatio);
        }

        /// <summary>Reference-counted so overlapping sources (dodge, cutscene, respawn) don't cancel each other.</summary>
        public void AddInvulnerability() => invulnerabilityCount++;

        public void RemoveInvulnerability() => invulnerabilityCount = Mathf.Max(0, invulnerabilityCount - 1);

        void OnValidate()
        {
            if (maxHealth < 1f) maxHealth = 1f;
        }
    }
}
