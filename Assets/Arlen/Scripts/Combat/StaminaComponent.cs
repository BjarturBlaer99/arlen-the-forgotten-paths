using System;
using Arlen.Core;
using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>Reusable stamina with regeneration delay. Thin MonoBehaviour wrapper around <see cref="StaminaPool"/>.</summary>
    [DisallowMultipleComponent]
    public class StaminaComponent : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxStamina = 100f;
        [SerializeField, Min(0f)] float regenPerSecond = 35f;
        [Tooltip("Seconds after spending stamina before regeneration starts.")]
        [SerializeField, Min(0f)] float regenDelay = 0.8f;
        [Tooltip("After being fully drained (e.g. by sprinting), stamina must recover to this fraction before draining again.")]
        [SerializeField, Range(0f, 1f)] float exhaustionRecoverFraction = 0.3f;

        StaminaPool pool;

        public float Max => Pool.Max;
        public float Current => Pool.Current;
        public float Normalized => Pool.Normalized;
        public bool IsExhausted => Pool.IsExhausted;

        /// <summary>(current, max)</summary>
        public event Action<float, float> StaminaChanged;
        public event Action Exhausted;
        public event Action Recovered;
        /// <summary>Raised when an action failed because there wasn't enough stamina (useful for UI flashes).</summary>
        public event Action SpendFailed;

        StaminaPool Pool
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
            pool = new StaminaPool(maxStamina, regenPerSecond, regenDelay, exhaustionRecoverFraction);
            pool.Changed += (c, m) => StaminaChanged?.Invoke(c, m);
            pool.Exhausted += () => Exhausted?.Invoke();
            pool.Recovered += () => Recovered?.Invoke();
        }

        void Update() => Pool.Tick(Time.deltaTime);

        public bool CanSpend(float amount) => Pool.CanSpend(amount);

        public bool TrySpend(float amount)
        {
            if (Pool.TrySpend(amount)) return true;
            SpendFailed?.Invoke();
            return false;
        }

        /// <summary>Continuous drain; returns false when the action should stop.</summary>
        public bool Drain(float amount) => Pool.Drain(amount);

        public void Refill() => Pool.Refill();

        /// <summary>Changes max stamina (equipment bonuses).</summary>
        public void SetMaxStamina(float max)
        {
            maxStamina = Mathf.Max(1f, max);
            Pool.SetMax(maxStamina);
        }

        void OnValidate()
        {
            if (pool == null) return;
            pool.RegenPerSecond = regenPerSecond;
            pool.RegenDelay = regenDelay;
            pool.ExhaustionRecoverFraction = exhaustionRecoverFraction;
            if (!Mathf.Approximately(pool.Max, maxStamina)) pool.SetMax(maxStamina);
        }
    }
}
