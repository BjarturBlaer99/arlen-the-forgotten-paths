using System;
using Arlen.Combat;
using Arlen.Core;
using UnityEngine;

namespace Arlen.Player
{
    public enum LocomotionMode
    {
        Idle,
        Walk,
        Run,
        Sprint,
    }

    /// <summary>
    /// Third-person locomotion on top of Unity's CharacterController: walk/run/sprint, jumping with
    /// coyote time and input buffering, gravity and camera-relative movement.
    ///
    /// Committed actions (dodge, attack) don't move the character themselves; they call
    /// <see cref="SetOverrideVelocity"/> each frame and the controller applies it. This keeps a single
    /// place that calls CharacterController.Move, which later traversal (climb, swim) can extend.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class ArlenPlayerController : MonoBehaviour
    {
        [Header("References (auto-resolved if empty)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] StaminaComponent stamina;
        [Tooltip("Movement is relative to this transform's yaw. Defaults to Camera.main.")]
        [SerializeField] Transform cameraTransform;

        [Header("Speeds (m/s)")]
        [SerializeField, Min(0f)] float walkSpeed = 2.4f;
        [SerializeField, Min(0f)] float runSpeed = 5.6f;
        [SerializeField, Min(0f)] float sprintSpeed = 8.6f;
        [Tooltip("Analog stick magnitude below this walks instead of runs.")]
        [SerializeField, Range(0f, 1f)] float walkInputThreshold = 0.5f;
        [SerializeField, Min(0f)] float acceleration = 45f;
        [SerializeField, Min(0f)] float deceleration = 60f;
        [Tooltip("Fraction of ground acceleration available in the air.")]
        [SerializeField, Range(0f, 1f)] float airControl = 0.45f;
        [Tooltip("Degrees per second the character turns toward the move direction.")]
        [SerializeField, Min(0f)] float turnSpeed = 1080f;

        [Header("Sprint")]
        [SerializeField, Min(0f)] float sprintStaminaPerSecond = 16f;

        [Header("Jump & Gravity")]
        [SerializeField, Min(0f)] float jumpHeight = 1.35f;
        [SerializeField] float gravity = -26f;
        [Tooltip("Extra gravity while falling makes jumps feel snappier.")]
        [SerializeField, Min(1f)] float fallGravityMultiplier = 1.6f;
        [SerializeField] float terminalVelocity = -40f;
        [Tooltip("Jump is still allowed this long after walking off a ledge.")]
        [SerializeField, Min(0f)] float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] float jumpStaminaCost = 0f;
        [Tooltip("Small constant downward speed while grounded keeps the controller snapped to slopes.")]
        [SerializeField] float groundedStickVelocity = -3f;

        CharacterController cc;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        bool hasOverride;
        Vector3 overrideVelocity;

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public LocomotionMode Mode { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity;
        public float VerticalVelocity => verticalVelocity;
        public float PlanarSpeed => planarVelocity.magnitude;
        public float RunSpeed => runSpeed;
        public float SprintSpeed => sprintSpeed;
        /// <summary>Camera-relative input direction of the current frame (zero when idle).</summary>
        public Vector3 DesiredDirection { get; private set; }

        public event Action Jumped;
        /// <summary>Raised on touchdown with the downward speed at impact (positive).</summary>
        public event Action<float> Landed;

        public Transform CameraTransform
        {
            get => cameraTransform;
            set => cameraTransform = value;
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (stamina == null) stamina = GetComponent<StaminaComponent>();
            if (input == null) Debug.LogError($"{nameof(ArlenPlayerController)} requires a {nameof(PlayerInputReader)}.", this);
        }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void OnDisable()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            hasOverride = false;
            IsSprinting = false;
        }

        /// <summary>
        /// Called by committed actions (dodge, attack lunge) each frame to dictate planar velocity.
        /// Must be called before this component's Update (actions use an earlier execution order).
        /// </summary>
        public void SetOverrideVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            overrideVelocity = velocity;
            hasOverride = true;
        }

        /// <summary>Instantly turns the character to face a planar direction.</summary>
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>Converts the current move input to a camera-relative world direction.</summary>
        public Vector3 GetCameraRelativeInput()
        {
            if (input == null) return Vector3.zero;
            float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            return MovementMath.CameraRelative(input.Move, yaw);
        }

        /// <summary>Moves the character to a position, e.g. for respawning. Resets velocity.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false; // CharacterController overrides transform changes while enabled.
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = wasEnabled;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool wasGrounded = IsGrounded;
            IsGrounded = cc.isGrounded;
            if (IsGrounded) lastGroundedTime = Time.time;

            bool busy = actionState != null && actionState.IsBusy;
            Vector3 inputDir = busy ? Vector3.zero : GetCameraRelativeInput();
            DesiredDirection = inputDir;
            float inputMagnitude = inputDir.magnitude;

            UpdatePlanarVelocity(inputDir, inputMagnitude, busy, dt);
            UpdateJump(busy);
            UpdateGravity(dt);
            UpdateRotation(inputDir, busy, dt);

            float fallSpeed = -verticalVelocity;
            CollisionFlags flags = cc.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            bool groundedNow = cc.isGrounded;
            if (groundedNow && !wasGrounded && fallSpeed > 0f)
            {
                IsGrounded = true;
                Landed?.Invoke(fallSpeed);
            }
        }

        void UpdatePlanarVelocity(Vector3 inputDir, float inputMagnitude, bool busy, float dt)
        {
            if (hasOverride)
            {
                planarVelocity = overrideVelocity;
                hasOverride = false;
                IsSprinting = false;
                Mode = LocomotionMode.Idle;
                return;
            }

            bool hasInput = inputMagnitude > 0.1f;
            bool wantsSprint = !busy && hasInput && input != null && input.SprintHeld;

            // Keep sprint momentum in the air, but only pay stamina on the ground.
            if (wantsSprint && IsGrounded)
                IsSprinting = stamina == null || stamina.Drain(sprintStaminaPerSecond * dt);
            else if (!wantsSprint)
                IsSprinting = false;

            float targetSpeed;
            if (!hasInput)
            {
                targetSpeed = 0f;
                Mode = LocomotionMode.Idle;
            }
            else if (IsSprinting)
            {
                targetSpeed = sprintSpeed;
                Mode = LocomotionMode.Sprint;
            }
            else if (input.WalkToggled || inputMagnitude < walkInputThreshold)
            {
                targetSpeed = walkSpeed;
                Mode = LocomotionMode.Walk;
            }
            else
            {
                targetSpeed = runSpeed;
                Mode = LocomotionMode.Run;
            }

            Vector3 target = hasInput ? inputDir.normalized * targetSpeed : Vector3.zero;
            float control = IsGrounded ? 1f : airControl;
            // In the air with no input, keep momentum instead of braking.
            float decel = IsGrounded ? deceleration : 0f;
            planarVelocity = MovementMath.Accelerate(planarVelocity, target, acceleration * control, decel, dt);
        }

        void UpdateJump(bool busy)
        {
            bool canJump = !busy && Time.time - lastGroundedTime <= coyoteTime && verticalVelocity <= 0.01f;
            if (!canJump || input == null) return;
            if (stamina != null && jumpStaminaCost > 0f && !stamina.CanSpend(jumpStaminaCost)) return;
            if (!input.ConsumeJump()) return;

            if (stamina != null && jumpStaminaCost > 0f) stamina.TrySpend(jumpStaminaCost);
            verticalVelocity = MovementMath.JumpVelocity(jumpHeight, gravity);
            lastGroundedTime = float.NegativeInfinity; // consume coyote time
            IsGrounded = false;
            Jumped?.Invoke();
        }

        void UpdateGravity(float dt)
        {
            if (IsGrounded && verticalVelocity <= 0f)
            {
                verticalVelocity = groundedStickVelocity;
                return;
            }

            // Leaving the ground without jumping: drop the stick velocity so we don't start falling fast.
            if (verticalVelocity == groundedStickVelocity) verticalVelocity = 0f;

            float g = verticalVelocity < 0f ? gravity * fallGravityMultiplier : gravity;
            verticalVelocity = Mathf.Max(verticalVelocity + g * dt, terminalVelocity);
        }

        void UpdateRotation(Vector3 inputDir, bool busy, float dt)
        {
            if (busy || inputDir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(inputDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }

        void OnValidate()
        {
            if (gravity > -0.1f) gravity = -0.1f;
            if (terminalVelocity > -1f) terminalVelocity = -1f;
            runSpeed = Mathf.Max(runSpeed, walkSpeed);
            sprintSpeed = Mathf.Max(sprintSpeed, runSpeed);
        }
    }
}
