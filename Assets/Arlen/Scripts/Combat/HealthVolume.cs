using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>
    /// Box-shaped area that periodically damages or heals everything inside it
    /// (e.g. Blight pools, healing springs). Uses overlap queries, so it works with
    /// CharacterControllers and needs no Rigidbody or trigger setup.
    /// </summary>
    public class HealthVolume : MonoBehaviour
    {
        public enum Mode { Damage, Heal }

        [SerializeField] Mode mode = Mode.Damage;
        [SerializeField, Min(0f)] float amountPerTick = 8f;
        [SerializeField, Min(0.05f)] float tickInterval = 0.5f;
        [Tooltip("Local-space size of the area, centred on this transform.")]
        [SerializeField] Vector3 size = new Vector3(4f, 2f, 4f);
        [SerializeField] LayerMask mask = ~0;

        readonly Collider[] buffer = new Collider[16];
        float timer;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < tickInterval) return;
            timer -= tickInterval;

            Vector3 center = transform.TransformPoint(new Vector3(0f, size.y * 0.5f, 0f));
            Vector3 halfExtents = Vector3.Scale(size * 0.5f, transform.lossyScale);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, transform.rotation, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var health = buffer[i].GetComponentInParent<HealthComponent>();
                if (health == null || !health.IsAlive) continue;
                // A target with several colliders should only tick once: only react to its first collider.
                if (!IsFirstColliderOf(health, i)) continue;

                if (mode == Mode.Heal) health.Heal(amountPerTick);
                else
                {
                    Vector3 dir = health.transform.position - transform.position;
                    dir.y = 0f;
                    health.ApplyDamage(new DamageInfo(amountPerTick, gameObject, buffer[i].bounds.center, dir.normalized));
                }
            }
        }

        bool IsFirstColliderOf(HealthComponent health, int index)
        {
            for (int j = 0; j < index; j++)
                if (buffer[j].GetComponentInParent<HealthComponent>() == health) return false;
            return true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = mode == Mode.Heal ? new Color(0.3f, 1f, 0.5f, 0.35f) : new Color(0.7f, 0.2f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
        }
    }
}
