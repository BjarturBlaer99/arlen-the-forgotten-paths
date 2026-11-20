using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>Describes a single hit. Passed by value; contains no allocations.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        /// <summary>Direction the hit travels (from attacker toward target), normalized on the XZ plane.</summary>
        public readonly Vector3 Direction;
        /// <summary>Melee strikes can be parried; hazards and projectiles-to-come generally cannot.</summary>
        public readonly bool Parryable;

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 direction, bool parryable = false)
        {
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            Direction = direction;
            Parryable = parryable;
        }
    }

    /// <summary>Outcome of a damage attempt, so attackers can react (e.g. stagger when parried).</summary>
    public enum DamageResult
    {
        /// <summary>Health was reduced.</summary>
        Applied,
        /// <summary>Target was dead, invulnerable (dodge i-frames) or the amount was zero.</summary>
        Ignored,
        /// <summary>Deflected by a timed parry; the attacker should be staggered.</summary>
        Parried,
    }

    /// <summary>Anything that can receive damage: players, enemies, breakables.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        DamageResult ApplyDamage(in DamageInfo info);
    }

    /// <summary>
    /// Gets a chance to handle a hit before it reaches health (parry now; block/armor later).
    /// Return <see cref="DamageResult.Applied"/> to let the hit through.
    /// </summary>
    public interface IDamageInterceptor
    {
        DamageResult Intercept(in DamageInfo info);
    }
}
