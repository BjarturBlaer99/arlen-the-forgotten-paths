using System;
using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>
    /// Test hazard for practising dodge timing: telegraphs, then deals one burst of damage in a radius.
    /// Dodging through the burst (i-frames) avoids the damage. Visual feedback is driven via events/state.
    /// </summary>
    public class PulsingHazard : MonoBehaviour
    {
        [SerializeField, Min(0f)] float damage = 25f;
        [SerializeField, Min(0.1f)] float radius = 3f;
        [SerializeField, Min(0.1f)] float interval = 3f;
        [Tooltip("Warning time before each burst.")]
        [SerializeField, Min(0f)] float telegraphTime = 0.9f;
        [SerializeField] LayerMask mask = ~0;
        [Tooltip("Optional renderer tinted while telegraphing.")]
        [SerializeField] Renderer indicator;
        [SerializeField] Color idleColor = new Color(0.35f, 0.15f, 0.45f);
        [SerializeField] Color warningColor = new Color(1f, 0.25f, 0.6f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly Collider[] buffer = new Collider[16];
        MaterialPropertyBlock block;
        float timer;

        public float Radius => radius;
        /// <summary>0 when idle, rises to 1 at the moment of the burst.</summary>
        public float Telegraph { get; private set; }
        public event Action Pulsed;

        void Awake() => block = new MaterialPropertyBlock();

        void Update()
        {
            timer += Time.deltaTime;
            float warnStart = interval - telegraphTime;
            Telegraph = timer > warnStart && telegraphTime > 0f ? Mathf.Clamp01((timer - warnStart) / telegraphTime) : 0f;
            UpdateIndicator();

            if (timer < interval) return;
            timer = 0f;
            Pulse();
        }

        void Pulse()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || AlreadyHit(target, i)) continue;
                Vector3 dir = buffer[i].bounds.center - transform.position;
                dir.y = 0f;
                target.ApplyDamage(new DamageInfo(damage, gameObject, buffer[i].bounds.center, dir.normalized));
            }
            Pulsed?.Invoke();
        }

        bool AlreadyHit(IDamageable target, int index)
        {
            for (int j = 0; j < index; j++)
                if (buffer[j].GetComponentInParent<IDamageable>() == target) return true;
            return false;
        }

        void UpdateIndicator()
        {
            if (indicator == null) return;
            // Flicker faster as the burst approaches.
            float flicker = Telegraph > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, Telegraph)) : 0f;
            Color c = Color.Lerp(idleColor, warningColor, Telegraph * (0.6f + 0.4f * flicker));
            indicator.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            indicator.SetPropertyBlock(block);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
