using System.Collections;
using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>
    /// Sparring target: has health, wobbles when hit (via an optional visual root), falls over on death
    /// and resets after a delay. Gameplay state lives in HealthComponent; this only drives presentation.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] Transform visualRoot;
        [SerializeField, Min(0f)] float resetDelay = 3f;
        [SerializeField, Min(0f)] float wobbleAngle = 18f;
        [SerializeField, Min(0.01f)] float wobbleDuration = 0.35f;

        HealthComponent health;
        Quaternion restRotation;
        Vector3 wobbleAxis;
        float wobbleTimer;
        bool fallen;

        void Awake()
        {
            health = GetComponent<HealthComponent>();
            if (visualRoot == null) visualRoot = transform;
            restRotation = visualRoot.localRotation;
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void OnDamaged(DamageInfo info)
        {
            Vector3 localDir = transform.InverseTransformDirection(info.Direction);
            wobbleAxis = Vector3.Cross(Vector3.up, localDir).normalized;
            if (wobbleAxis.sqrMagnitude < 0.01f) wobbleAxis = Vector3.right;
            wobbleTimer = wobbleDuration;
        }

        void OnDied() => StartCoroutine(FallAndReset());

        IEnumerator FallAndReset()
        {
            fallen = true;
            Quaternion fallen90 = restRotation * Quaternion.AngleAxis(80f, wobbleAxis);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
            {
                visualRoot.localRotation = Quaternion.Slerp(restRotation, fallen90, t * t);
                yield return null;
            }
            visualRoot.localRotation = fallen90;
            yield return new WaitForSeconds(resetDelay);
            visualRoot.localRotation = restRotation;
            fallen = false;
            health.Revive();
        }

        void Update()
        {
            if (fallen || wobbleTimer <= 0f) return;
            wobbleTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(wobbleTimer / wobbleDuration);
            float angle = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * wobbleAngle;
            visualRoot.localRotation = restRotation * Quaternion.AngleAxis(angle, wobbleAxis);
        }
    }
}
