using System;
using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>
    /// Timing and damage data for one swing of a combo. Phases: Windup → Active (hits register) → Recovery.
    /// During recovery the next combo input chains, and a dodge may cancel.
    /// </summary>
    [Serializable]
    public class MeleeAttackStep
    {
        public string name = "Slash";
        [Min(0f)] public float damage = 10f;
        [Min(0f)] public float staminaCost = 12f;

        [Header("Timing (seconds)")]
        [Min(0f)] public float windup = 0.12f;
        [Min(0.01f)] public float active = 0.12f;
        [Min(0f)] public float recovery = 0.32f;

        [Header("Hit Volume (local to the attacker)")]
        public Vector3 hitOffset = new Vector3(0f, 1f, 1.05f);
        [Min(0.05f)] public float hitRadius = 1.1f;

        [Header("Motion")]
        [Tooltip("Forward speed applied during windup and active phases.")]
        [Min(0f)] public float lungeSpeed = 3f;

        public float TotalDuration => windup + active + recovery;

        public MeleeAttackStep() { }

        public MeleeAttackStep(string name, float damage, float staminaCost, float windup, float active, float recovery, float lungeSpeed)
        {
            this.name = name;
            this.damage = damage;
            this.staminaCost = staminaCost;
            this.windup = windup;
            this.active = active;
            this.recovery = recovery;
            this.lungeSpeed = lungeSpeed;
        }
    }
}
