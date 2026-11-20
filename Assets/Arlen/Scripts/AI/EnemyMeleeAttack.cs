using System;
using System.Collections.Generic;
using Arlen.Combat;
using UnityEngine;

namespace Arlen.AI
{
    public enum EnemyAttackPhase
    {
        None,
        /// <summary>Telegraph. Tracks the target at a limited turn rate.</summary>
        Windup,
        /// <summary>Hits register.</summary>
        Active,
        Recovery,
    }

    /// <summary>
    /// Executes a single telegraphed melee attack. The brain decides when; this decides how.
    /// Only hostile <see cref="Targetable"/>s are hit (no friendly fire); a parried hit raises
    /// <see cref="Parried"/> so the brain can stagger.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyMeleeAttack : MonoBehaviour
    {
        [SerializeField, Range(4, 32)] int maxHitColliders = 8;
        [SerializeField] LayerMask hitMask = ~0;

        EnemyStats stats;
        EnemyMotor motor;
        Team team = Team.Enemy;
        Targetable target;
        Collider[] buffer;
        readonly List<IDamageable> hitThisSwing = new List<IDamageable>(4);
        float timer;

        public EnemyAttackPhase Phase { get; private set; }
        public bool IsAttacking => Phase != EnemyAttackPhase.None;
        public float PhaseProgress { get; private set; }

        public event Action Started;
        public event Action<EnemyAttackPhase> PhaseChanged;
        /// <summary>(target, result) for every target the swing connected with.</summary>
        public event Action<IDamageable, DamageResult> HitResolved;
        public event Action Parried;
        public event Action Finished;

        public void Initialize(EnemyStats enemyStats, EnemyMotor enemyMotor, Team ownTeam)
        {
            stats = enemyStats;
            motor = enemyMotor;
            team = ownTeam;
            buffer ??= new Collider[maxHitColliders];
        }

        void Awake() => buffer ??= new Collider[maxHitColliders];

        void OnDisable() => Cancel();

        public void Begin(Targetable attackTarget)
        {
            if (stats == null || IsAttacking) return;
            target = attackTarget;
            hitThisSwing.Clear();
            if (motor != null) motor.Stop();
            SetPhase(EnemyAttackPhase.Windup);
            Started?.Invoke();
        }

        /// <summary>Aborts the attack (stagger, death). Does not raise Finished.</summary>
        public void Cancel()
        {
            if (!IsAttacking) return;
            SetPhase(EnemyAttackPhase.None);
            target = null;
        }

        void Update()
        {
            if (!IsAttacking) return;
            timer += Time.deltaTime;

            switch (Phase)
            {
                case EnemyAttackPhase.Windup:
                    PhaseProgress = stats.attackWindup > 0f ? Mathf.Clamp01(timer / stats.attackWindup) : 1f;
                    if (target != null && motor != null) motor.FaceTowards(target.transform.position, stats.windupTrackingSpeed);
                    if (timer >= stats.attackWindup) SetPhase(EnemyAttackPhase.Active);
                    break;

                case EnemyAttackPhase.Active:
                    PhaseProgress = Mathf.Clamp01(timer / stats.attackActive);
                    if (motor != null) motor.Push(transform.forward * stats.attackLungeSpeed);
                    if (DetectHits()) return; // parried: brain has cancelled us
                    if (timer >= stats.attackActive) SetPhase(EnemyAttackPhase.Recovery);
                    break;

                case EnemyAttackPhase.Recovery:
                    PhaseProgress = stats.attackRecovery > 0f ? Mathf.Clamp01(timer / stats.attackRecovery) : 1f;
                    if (timer >= stats.attackRecovery)
                    {
                        SetPhase(EnemyAttackPhase.None);
                        target = null;
                        Finished?.Invoke();
                    }
                    break;
            }
        }

        /// <summary>Returns true if the attack was parried.</summary>
        bool DetectHits()
        {
            Vector3 center = transform.TransformPoint(stats.attackHitOffset);
            int count = Physics.OverlapSphereNonAlloc(center, stats.attackHitRadius, buffer, hitMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;

                var targetable = col.GetComponentInParent<Targetable>();
                if (targetable == null || !Targetable.AreHostile(team, targetable.Team)) continue;
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || hitThisSwing.Contains(damageable)) continue;
                hitThisSwing.Add(damageable);

                Vector3 dir = targetable.transform.position - transform.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
                var info = new DamageInfo(stats.attackDamage, gameObject, col.ClosestPoint(center), dir, stats.attackParryable);
                DamageResult result = damageable.ApplyDamage(info);
                HitResolved?.Invoke(damageable, result);

                if (result == DamageResult.Parried)
                {
                    Parried?.Invoke();
                    return true;
                }
            }
            return false;
        }

        void SetPhase(EnemyAttackPhase phase)
        {
            Phase = phase;
            timer = 0f;
            PhaseProgress = 0f;
            PhaseChanged?.Invoke(phase);
        }

        void OnDrawGizmosSelected()
        {
            if (stats == null) return;
            Gizmos.color = Phase == EnemyAttackPhase.Active ? Color.red : new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.TransformPoint(stats.attackHitOffset), stats.attackHitRadius);
        }
    }
}
