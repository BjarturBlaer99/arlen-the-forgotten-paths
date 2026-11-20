using System;
using Arlen.Combat;
using UnityEngine;

namespace Arlen.AI
{
    /// <summary>
    /// Perception: finds hostile <see cref="Targetable"/>s by distance, field of view and line of sight.
    /// Scans on an interval rather than every frame. Once engaged, keeps the target until it dies,
    /// gets too far away, or the brain calls <see cref="Forget"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySensor : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] float scanInterval = 0.2f;
        [SerializeField] Vector3 eyeOffset = new Vector3(0f, 1.5f, 0f);
        [Tooltip("Layers that block sight.")]
        [SerializeField] LayerMask obstacleMask = ~0;

        EnemyStats stats;
        Team team = Team.Enemy;
        float scanTimer;

        public Targetable CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null && CurrentTarget.isActiveAndEnabled && CurrentTarget.IsAlive;
        /// <summary>When true no new targets are acquired (e.g. while returning home).</summary>
        public bool Suppressed { get; set; }

        public event Action<Targetable> TargetAcquired;
        public event Action TargetLost;

        public Vector3 EyePosition => transform.position + transform.rotation * eyeOffset;

        public void Initialize(EnemyStats enemyStats, Team ownTeam)
        {
            stats = enemyStats;
            team = ownTeam;
            scanTimer = UnityEngine.Random.Range(0f, scanInterval); // spread scans across enemies
        }

        void Update()
        {
            if (stats == null) return;
            scanTimer -= Time.deltaTime;
            if (scanTimer > 0f) return;
            scanTimer = scanInterval;
            Scan();
        }

        /// <summary>Runs perception immediately (also used by tests).</summary>
        public void Scan()
        {
            if (stats == null) return;

            if (CurrentTarget != null)
            {
                if (!HasTarget || PlanarDistance(CurrentTarget.transform.position) > stats.loseInterestRadius) Forget();
                else return;
            }

            if (Suppressed) return;

            Targetable best = null;
            float bestDist = float.MaxValue;
            var all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null || !t.IsAlive || !Targetable.AreHostile(team, t.Team)) continue;
                float d = PlanarDistance(t.transform.position);
                if (d > stats.detectionRadius || d >= bestDist) continue;
                if (d > stats.proximityRadius && !InFieldOfView(t.transform.position)) continue;
                if (!HasLineOfSight(t)) continue;
                best = t;
                bestDist = d;
            }

            if (best != null) SetTarget(best);
        }

        /// <summary>Immediately engages a target (e.g. when attacked from behind).</summary>
        public void SetTarget(Targetable target)
        {
            if (target == null || target == CurrentTarget) return;
            CurrentTarget = target;
            TargetAcquired?.Invoke(target);
        }

        public void Forget()
        {
            if (CurrentTarget == null) return;
            CurrentTarget = null;
            TargetLost?.Invoke();
        }

        public bool InFieldOfView(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(transform.forward, to) <= stats.fieldOfView * 0.5f;
        }

        public bool HasLineOfSight(Targetable target)
        {
            Vector3 eye = EyePosition;
            if (!Physics.Linecast(eye, target.AimPosition, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
                return true;
            // Hitting the target itself (or ourselves) doesn't block sight.
            return hit.transform.IsChildOf(target.transform) || hit.transform.IsChildOf(transform);
        }

        float PlanarDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void OnDrawGizmosSelected()
        {
            if (stats == null) return;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, stats.proximityRadius);
        }
    }
}
