using System;
using UnityEngine;

namespace Arlen.Core
{
    /// <summary>
    /// Engine-independent health model. Wrapped by <see cref="Arlen.Combat.HealthComponent"/>
    /// so the rules can be unit-tested without scenes.
    /// </summary>
    public sealed class HealthPool
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Normalized => Max > 0f ? Current / Max : 0f;

        /// <summary>Raised whenever Current or Max changes: (current, max).</summary>
        public event Action<float, float> Changed;
        /// <summary>Raised with the amount actually removed.</summary>
        public event Action<float> Damaged;
        /// <summary>Raised with the amount actually restored.</summary>
        public event Action<float> Healed;
        public event Action Died;
        public event Action Revived;

        public HealthPool(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max health must be positive.");
            Max = max;
            Current = max;
        }

        /// <summary>Removes health. Returns the amount actually removed (0 if dead or amount &lt;= 0).</summary>
        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;

            float applied = Mathf.Min(amount, Current);
            Current -= applied;
            Damaged?.Invoke(applied);
            Changed?.Invoke(Current, Max);
            if (IsDead) Died?.Invoke();
            return applied;
        }

        /// <summary>Restores health. Dead entities cannot be healed; use <see cref="Revive"/>.</summary>
        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;

            float applied = Mathf.Min(amount, Max - Current);
            if (applied <= 0f) return 0f;
            Current += applied;
            Healed?.Invoke(applied);
            Changed?.Invoke(Current, Max);
            return applied;
        }

        /// <summary>Changes max health. Optionally scales current health to keep the same ratio.</summary>
        public void SetMax(float max, bool keepRatio = true)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max health must be positive.");
            float ratio = Normalized;
            Max = max;
            Current = keepRatio ? max * ratio : Mathf.Min(Current, max);
            Changed?.Invoke(Current, Max);
        }

        /// <summary>Restores to full (or a fraction of) health, raising Revived if previously dead.</summary>
        public void Revive(float fraction = 1f)
        {
            bool wasDead = IsDead;
            Current = Mathf.Clamp(Max * fraction, 1f, Max);
            Changed?.Invoke(Current, Max);
            if (wasDead) Revived?.Invoke();
        }
    }
}
