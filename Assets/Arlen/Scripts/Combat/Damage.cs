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

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 direction)
        {
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            Direction = direction;
        }
    }

    /// <summary>Anything that can receive damage: players, enemies, breakables.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        /// <summary>Returns true if the damage was accepted (not blocked, dodged or ignored).</summary>
        bool ApplyDamage(in DamageInfo info);
    }
}
