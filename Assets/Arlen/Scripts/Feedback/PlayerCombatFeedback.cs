using Arlen.Combat;
using UnityEngine;

namespace Arlen.Feedback
{
    /// <summary>
    /// Connects player combat events to juice: hit-stop, camera shake and impact flashes.
    /// All effects are optional references; gameplay works identically without this component.
    /// </summary>
    public class PlayerCombatFeedback : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] HealthComponent playerHealth;
        [SerializeField] PlayerMeleeCombat combat;
        [SerializeField] PlayerParry parry;

        [Header("Effects")]
        [SerializeField] HitStop hitStop;
        [SerializeField] CameraShake cameraShake;
        [SerializeField] ImpactFxPool impacts;

        [Header("Player lands a hit")]
        [SerializeField] float hitStopOnHit = 0.05f;
        [SerializeField] float hitStopFinisher = 0.09f;
        [SerializeField] float shakeOnHit = 0.18f;
        [SerializeField] Color hitColor = new Color(1f, 0.9f, 0.6f);

        [Header("Player is hit")]
        [SerializeField] float hitStopOnHurt = 0.07f;
        [SerializeField] float shakeOnHurt = 0.45f;
        [SerializeField] Color hurtColor = new Color(1f, 0.3f, 0.25f);

        [Header("Parry")]
        [SerializeField] float hitStopOnParry = 0.14f;
        [SerializeField] float shakeOnParry = 0.35f;
        [SerializeField] Color parryColor = new Color(0.55f, 0.95f, 1f);

        void OnEnable()
        {
            if (combat != null) combat.TargetHit += OnTargetHit;
            if (playerHealth != null) playerHealth.Damaged += OnHurt;
            if (parry != null) parry.Parried += OnParried;
        }

        void OnDisable()
        {
            if (combat != null) combat.TargetHit -= OnTargetHit;
            if (playerHealth != null) playerHealth.Damaged -= OnHurt;
            if (parry != null) parry.Parried -= OnParried;
        }

        void OnTargetHit(IDamageable target, DamageInfo info)
        {
            bool finisher = combat != null && combat.StepIndex == combat.ComboLength - 1;
            if (hitStop != null) hitStop.Trigger(finisher ? hitStopFinisher : hitStopOnHit);
            if (cameraShake != null) cameraShake.Add(finisher ? shakeOnHit * 1.6f : shakeOnHit);
            if (impacts != null) impacts.Spawn(info.HitPoint, hitColor, finisher ? 0.7f : 0.5f);
        }

        void OnHurt(DamageInfo info)
        {
            // Small environmental ticks (Blight pools) only get a light shake.
            bool heavy = info.Amount >= 10f;
            if (heavy && hitStop != null) hitStop.Trigger(hitStopOnHurt);
            if (cameraShake != null) cameraShake.Add(heavy ? shakeOnHurt : shakeOnHurt * 0.2f);
            if (heavy && impacts != null) impacts.Spawn(info.HitPoint, hurtColor, 0.6f);
        }

        void OnParried(DamageInfo info)
        {
            if (hitStop != null) hitStop.Trigger(hitStopOnParry);
            if (cameraShake != null) cameraShake.Add(shakeOnParry);
            if (impacts != null) impacts.Spawn(info.HitPoint, parryColor, 1.1f);
        }
    }
}
