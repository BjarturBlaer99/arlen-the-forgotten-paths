using System;
using Arlen.Combat;
using Arlen.Core;
using UnityEngine;

namespace Arlen.Player
{
    /// <summary>
    /// Directional dodge roll: camera-relative toward the move input, or a backstep with no input.
    /// Costs stamina, grants a configurable invulnerability window and has a cooldown to prevent spamming.
    /// Can cancel the recovery of an attack (see <see cref="PlayerActionState"/>).
    /// </summary>
    [DefaultExecutionOrder(-10)] // must run before ArlenPlayerController to supply override velocity
    [DisallowMultipleComponent]
    public class PlayerDodge : MonoBehaviour
    {
        [Header("References (auto-resolved if empty)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] ArlenPlayerController controller;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] StaminaComponent stamina;
        [SerializeField] HealthComponent health;

        [Header("Roll")]
        [SerializeField, Min(0.1f)] float distance = 4.2f;
        [SerializeField, Min(0.05f)] float duration = 0.42f;
        [Tooltip("Speed profile over the roll (x: 0..1 normalized time). Rescaled so total distance stays exact.")]
        [SerializeField] AnimationCurve speedCurve = new AnimationCurve(
            new Keyframe(0f, 1.2f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0.15f));
        [SerializeField, Min(0f)] float backstepDistanceMultiplier = 0.6f;
        [SerializeField] bool allowInAir = false;

        [Header("Cost & Timing")]
        [SerializeField, Min(0f)] float staminaCost = 22f;
        [Tooltip("Time after a dodge ends before another can start.")]
        [SerializeField, Min(0f)] float cooldown = 0.18f;
        [Tooltip("Invulnerability window in normalized roll time.")]
        [SerializeField] Vector2 invulnerabilityWindow = new Vector2(0.05f, 0.7f);

        float timer;
        float cooldownTimer;
        float curveAverage = 1f;
        float currentDistance;
        Vector3 direction;
        bool invulnerable;

        public bool IsDodging { get; private set; }
        public bool IsBackstep { get; private set; }
        public bool IsInvulnerable => invulnerable;
        public float Duration => duration;
        /// <summary>0..1 progress of the current dodge.</summary>
        public float NormalizedTime => IsDodging ? Mathf.Clamp01(timer / duration) : 0f;
        public Vector3 Direction => direction;

        public event Action Started;
        public event Action Finished;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (stamina == null) stamina = GetComponent<StaminaComponent>();
            if (health == null) health = GetComponent<HealthComponent>();
            curveAverage = MovementMath.CurveAverage(speedCurve);

            if (controller == null || actionState == null)
                Debug.LogError($"{nameof(PlayerDodge)} requires {nameof(ArlenPlayerController)} and {nameof(PlayerActionState)}.", this);
        }

        void OnEnable()
        {
            if (actionState != null) actionState.Interrupted += OnInterrupted;
        }

        void OnDisable()
        {
            if (actionState != null) actionState.Interrupted -= OnInterrupted;
            if (IsDodging) Stop(raiseFinished: false);
        }

        void OnValidate() => curveAverage = MovementMath.CurveAverage(speedCurve);

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsDodging) Tick(dt);
            else
            {
                if (cooldownTimer > 0f) cooldownTimer -= dt;
                TryStartFromInput();
            }
        }

        void TryStartFromInput()
        {
            if (input == null || controller == null || actionState == null) return;
            if (cooldownTimer > 0f) return;
            if (!allowInAir && !controller.IsGrounded) return;
            // Only consume the press when a dodge is plausible, so it stays buffered during attacks.
            if (actionState.IsBusy && !actionState.IsCancelable) return;
            if (!input.ConsumeDodge()) return;

            if (stamina != null && !stamina.CanSpend(staminaCost))
            {
                stamina.TrySpend(staminaCost); // raises SpendFailed for UI feedback
                return;
            }
            if (!actionState.TryBegin(PlayerAction.Dodge)) return;
            if (stamina != null) stamina.TrySpend(staminaCost);

            Vector3 inputDir = controller.GetCameraRelativeInput();
            IsBackstep = inputDir.sqrMagnitude < 0.01f;
            if (IsBackstep)
            {
                direction = -transform.forward;
                currentDistance = distance * backstepDistanceMultiplier;
            }
            else
            {
                direction = inputDir.normalized;
                currentDistance = distance;
                controller.FaceDirection(direction);
            }

            timer = 0f;
            IsDodging = true;
            Started?.Invoke();
            Tick(0f);
        }

        void Tick(float dt)
        {
            timer += dt;
            float t = Mathf.Clamp01(timer / duration);

            bool shouldBeInvulnerable = t >= invulnerabilityWindow.x && t <= invulnerabilityWindow.y;
            SetInvulnerable(shouldBeInvulnerable);

            if (timer >= duration)
            {
                Stop(raiseFinished: true);
                return;
            }

            float baseSpeed = currentDistance / duration;
            float speed = baseSpeed * speedCurve.Evaluate(t) / curveAverage;
            controller.SetOverrideVelocity(direction * speed);
        }

        void Stop(bool raiseFinished)
        {
            SetInvulnerable(false);
            IsDodging = false;
            cooldownTimer = cooldown;
            if (actionState != null) actionState.End(PlayerAction.Dodge);
            if (raiseFinished) Finished?.Invoke();
        }

        void OnInterrupted(PlayerAction action)
        {
            if (action == PlayerAction.Dodge && IsDodging) Stop(raiseFinished: false);
        }

        void SetInvulnerable(bool value)
        {
            if (invulnerable == value || health == null) { invulnerable = value && health != null; return; }
            invulnerable = value;
            if (value) health.AddInvulnerability();
            else health.RemoveInvulnerability();
        }
    }
}
