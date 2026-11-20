using UnityEngine;

namespace Arlen.AI
{
    /// <summary>
    /// Tunable data for one enemy type. Shared by every instance, so balance changes apply everywhere
    /// and new enemies (Ruffian Brute, Dire Wolf, ...) are mostly new assets rather than new code.
    /// </summary>
    [CreateAssetMenu(menuName = "Arlen/Enemy Stats", fileName = "EnemyStats")]
    public class EnemyStats : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Enemy";

        [Header("Health")]
        [Min(1f)] public float maxHealth = 80f;
        [Tooltip("Damage absorbed before a hit causes a flinch-stagger. Resets after staggering or idling.")]
        [Min(0f)] public float poise = 25f;
        [Min(0f)] public float poiseRegenDelay = 2f;

        [Header("Movement")]
        [Min(0f)] public float wanderSpeed = 1.6f;
        [Min(0f)] public float chaseSpeed = 4.2f;
        [Tooltip("Degrees per second when turning to face the target outside of navigation.")]
        [Min(0f)] public float turnSpeed = 360f;
        [Min(0f)] public float wanderRadius = 3.5f;
        public Vector2 wanderPause = new Vector2(1.5f, 4f);

        [Header("Perception")]
        [Min(0f)] public float detectionRadius = 12f;
        [Range(0f, 360f)] public float fieldOfView = 160f;
        [Tooltip("Within this distance the enemy notices the target regardless of facing.")]
        [Min(0f)] public float proximityRadius = 3.5f;
        [Min(0f)] public float loseInterestRadius = 20f;
        [Tooltip("Max distance from the home position before giving up the chase.")]
        [Min(0f)] public float leashRadius = 28f;
        [Tooltip("Pause after first spotting the target (alert reaction).")]
        [Min(0f)] public float alertDelay = 0.5f;

        [Header("Melee Attack")]
        [Min(0f)] public float attackRange = 2f;
        [Min(0f)] public float attackDamage = 18f;
        [Tooltip("Telegraph: raises the weapon. The player's cue to dodge or parry.")]
        [Min(0f)] public float attackWindup = 0.65f;
        [Min(0.01f)] public float attackActive = 0.15f;
        [Min(0f)] public float attackRecovery = 0.8f;
        public Vector2 attackCooldown = new Vector2(1.0f, 2.0f);
        public Vector3 attackHitOffset = new Vector3(0f, 1f, 1.2f);
        [Min(0.05f)] public float attackHitRadius = 1.0f;
        [Min(0f)] public float attackLungeSpeed = 2.5f;
        [Tooltip("Degrees per second the enemy can still track the target during windup.")]
        [Min(0f)] public float windupTrackingSpeed = 200f;
        public bool attackParryable = true;

        [Header("Stagger")]
        [Min(0f)] public float hitStaggerDuration = 0.5f;
        [Min(0f)] public float parryStaggerDuration = 1.6f;

        [Header("Death")]
        [Min(0f)] public float corpseDuration = 4f;

        void OnValidate()
        {
            if (attackCooldown.y < attackCooldown.x) attackCooldown.y = attackCooldown.x;
            if (wanderPause.y < wanderPause.x) wanderPause.y = wanderPause.x;
            loseInterestRadius = Mathf.Max(loseInterestRadius, detectionRadius);
        }
    }
}
