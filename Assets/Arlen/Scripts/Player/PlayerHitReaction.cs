using System;
using Arlen.Combat;
using UnityEngine;

namespace Arlen.Player
{
    /// <summary>
    /// Heavy hits interrupt whatever the player is doing and push them back briefly (hit-stun).
    /// Light ticks (Blight pools) don't stun. Grants a short grace period of invulnerability after
    /// a stun so one enemy can't chain hits.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public class PlayerHitReaction : MonoBehaviour
    {
        [SerializeField] HealthComponent health;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] ArlenPlayerController controller;

        [Tooltip("Hits below this damage don't cause hit-stun.")]
        [SerializeField, Min(0f)] float minDamageToStun = 10f;
        [SerializeField, Min(0f)] float stunDuration = 0.35f;
        [SerializeField, Min(0f)] float knockbackSpeed = 5f;
        [Tooltip("Invulnerability after the stun ends.")]
        [SerializeField, Min(0f)] float graceInvulnerability = 0.4f;

        float timer;
        float graceTimer;
        bool graceActive;
        Vector3 knockDirection;

        public bool IsStunned { get; private set; }
        public event Action Stunned;

        void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
        }

        void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
            if (actionState != null) actionState.Interrupted += OnInterrupted;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (actionState != null) actionState.Interrupted -= OnInterrupted;
            IsStunned = false;
            SetGrace(false);
        }

        void OnDamaged(DamageInfo info)
        {
            if (info.Amount < minDamageToStun || actionState == null) return;
            if (info.Amount >= health.Current) return; // lethal: death handling takes over

            knockDirection = info.Direction;
            knockDirection.y = 0f;
            if (knockDirection.sqrMagnitude < 0.0001f) knockDirection = -transform.forward;
            knockDirection.Normalize();

            actionState.ForceBegin(PlayerAction.HitStun);
            IsStunned = true;
            timer = 0f;
            Stunned?.Invoke();
        }

        void Update()
        {
            if (graceActive)
            {
                graceTimer -= Time.deltaTime;
                if (graceTimer <= 0f) SetGrace(false);
            }

            if (!IsStunned) return;
            timer += Time.deltaTime;
            float t = stunDuration > 0f ? Mathf.Clamp01(timer / stunDuration) : 1f;
            if (controller != null) controller.SetOverrideVelocity(knockDirection * knockbackSpeed * (1f - t) * (1f - t));

            if (timer >= stunDuration)
            {
                IsStunned = false;
                actionState.End(PlayerAction.HitStun);
                graceTimer = graceInvulnerability;
                SetGrace(graceInvulnerability > 0f);
            }
        }

        void OnInterrupted(PlayerAction action)
        {
            if (action == PlayerAction.HitStun) IsStunned = false;
        }

        void SetGrace(bool value)
        {
            if (graceActive == value || health == null) return;
            graceActive = value;
            if (value) health.AddInvulnerability();
            else health.RemoveInvulnerability();
        }
    }
}
