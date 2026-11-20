using System;
using UnityEngine;

namespace Arlen.Core
{
    /// <summary>
    /// Engine-independent stamina model with regeneration delay and an exhaustion lockout.
    /// Discrete actions (dodge, attack) use <see cref="TrySpend"/>; continuous actions (sprint) use <see cref="Drain"/>.
    /// </summary>
    public sealed class StaminaPool
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public float RegenPerSecond { get; set; }
        public float RegenDelay { get; set; }
        /// <summary>Fraction of Max that must be regained before an exhausted pool can be drained again.</summary>
        public float ExhaustionRecoverFraction { get; set; }

        /// <summary>True after the pool hit zero via Drain, until it recovers past ExhaustionRecoverFraction.</summary>
        public bool IsExhausted { get; private set; }
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsRegenerating => regenDelayTimer <= 0f && Current < Max;

        public event Action<float, float> Changed;
        public event Action Exhausted;
        public event Action Recovered;

        float regenDelayTimer;

        public StaminaPool(float max, float regenPerSecond, float regenDelay, float exhaustionRecoverFraction = 0.25f)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max stamina must be positive.");
            Max = max;
            Current = max;
            RegenPerSecond = Mathf.Max(0f, regenPerSecond);
            RegenDelay = Mathf.Max(0f, regenDelay);
            ExhaustionRecoverFraction = Mathf.Clamp01(exhaustionRecoverFraction);
        }

        public bool CanSpend(float amount) => amount <= 0f || Current >= amount;

        /// <summary>Spends a fixed cost only if the full amount is available.</summary>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return true;
            if (Current < amount) return false;
            SetCurrent(Current - amount);
            regenDelayTimer = RegenDelay;
            return true;
        }

        /// <summary>
        /// Continuous drain (e.g. sprinting). Returns false once the pool is empty or exhausted,
        /// signalling the caller to stop the action.
        /// </summary>
        public bool Drain(float amount)
        {
            if (IsExhausted) return false;
            if (amount <= 0f) return Current > 0f;

            regenDelayTimer = RegenDelay;
            SetCurrent(Mathf.Max(0f, Current - amount));
            if (Current <= 0f)
            {
                IsExhausted = true;
                Exhausted?.Invoke();
                return false;
            }
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (regenDelayTimer > 0f)
            {
                regenDelayTimer -= deltaTime;
                if (regenDelayTimer > 0f) return;
                // Use only the leftover part of this frame for regeneration.
                deltaTime = -regenDelayTimer;
                regenDelayTimer = 0f;
            }

            if (Current < Max)
                SetCurrent(Mathf.Min(Max, Current + RegenPerSecond * deltaTime));

            if (IsExhausted && Current >= Max * ExhaustionRecoverFraction)
            {
                IsExhausted = false;
                Recovered?.Invoke();
            }
        }

        public void Refill()
        {
            regenDelayTimer = 0f;
            SetCurrent(Max);
            if (IsExhausted)
            {
                IsExhausted = false;
                Recovered?.Invoke();
            }
        }

        public void SetMax(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max stamina must be positive.");
            Max = max;
            SetCurrent(Mathf.Min(Current, max));
        }

        void SetCurrent(float value)
        {
            if (Mathf.Approximately(value, Current)) { Current = value; return; }
            Current = value;
            Changed?.Invoke(Current, Max);
        }
    }
}
