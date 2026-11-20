using UnityEngine;
using UnityEngine.AI;

namespace Arlen.AI
{
    /// <summary>
    /// Movement wrapper around NavMeshAgent. The brain asks for destinations, facing and small
    /// pushes (lunges/knockback); this component owns all agent configuration.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public class EnemyMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] float acceleration = 24f;
        [Tooltip("Destination changes smaller than this don't trigger a re-path.")]
        [SerializeField, Min(0f)] float repathThreshold = 0.5f;

        NavMeshAgent agent;
        bool warnedOffMesh;

        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh;
        public float Speed => IsReady ? agent.velocity.magnitude : 0f;
        public Vector3 Velocity => IsReady ? agent.velocity : Vector3.zero;
        public bool HasArrived => IsReady && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        void Awake() => EnsureAgent();

        // Resolved lazily: the brain may call Initialize before this component's Awake has run.
        void EnsureAgent()
        {
            if (agent != null) return;
            agent = GetComponent<NavMeshAgent>();
            agent.acceleration = acceleration;
            agent.autoBraking = true;
        }

        public void Initialize(EnemyStats stats)
        {
            EnsureAgent();
            agent.angularSpeed = stats.turnSpeed;
            agent.speed = stats.wanderSpeed;
        }

        void OnEnable() => EnsureOnNavMesh();

        bool EnsureOnNavMesh()
        {
            if (agent == null || !agent.enabled) return false;
            if (agent.isOnNavMesh) return true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return agent.isOnNavMesh;
            }
            if (!warnedOffMesh)
            {
                warnedOffMesh = true;
                Debug.LogWarning($"[Arlen] '{name}' is not on a NavMesh; it can't move. Bake the scene's NavMeshSurface.", this);
            }
            return false;
        }

        public void MoveTo(Vector3 destination, float speed, float stoppingDistance = 0.2f)
        {
            if (!EnsureOnNavMesh()) return;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = speed;
            agent.stoppingDistance = stoppingDistance;
            if (!agent.hasPath || (agent.destination - destination).sqrMagnitude > repathThreshold * repathThreshold)
                agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!IsReady) return;
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        /// <summary>Rotates toward a point at a limited rate (navigation rotation is disabled meanwhile).</summary>
        public void FaceTowards(Vector3 point, float degreesPerSecond)
        {
            if (agent != null) agent.updateRotation = false;
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
        }

        public float AngleTo(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(transform.forward, dir);
        }

        /// <summary>Moves by a velocity this frame while staying on the NavMesh (lunges, knockback).</summary>
        public void Push(Vector3 velocity)
        {
            if (!IsReady) return;
            velocity.y = 0f;
            agent.Move(velocity * Time.deltaTime);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (agent != null && agent.enabled) agent.Warp(position);
            else transform.position = position;
            transform.rotation = rotation;
        }

        public void SetNavigationEnabled(bool value)
        {
            if (agent == null) return;
            if (!value && IsReady) Stop();
            agent.enabled = value;
            if (value) EnsureOnNavMesh();
        }
    }
}
