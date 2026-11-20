using System;
using System.Collections.Generic;
using Arlen.Player;
using UnityEngine;

namespace Arlen.Combat
{
    public enum AttackPhase
    {
        None,
        Windup,
        Active,
        Recovery,
    }

    /// <summary>
    /// Player sword combat: a configurable light-attack combo (three steps by default) with
    /// windup/active/recovery timing, input buffering, stamina cost and sphere-overlap hit detection.
    /// Visuals and animation listen to events/phase; this component never touches renderers or animators.
    /// </summary>
    [DefaultExecutionOrder(-10)] // must run before ArlenPlayerController to supply lunge velocity
    [DisallowMultipleComponent]
    public class PlayerMeleeCombat : MonoBehaviour
    {
        [Header("References (auto-resolved if empty)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] ArlenPlayerController controller;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] StaminaComponent stamina;

        [Header("Combo")]
        [SerializeField] MeleeAttackStep[] combo =
        {
            new MeleeAttackStep("Slash", 10f, 10f, 0.10f, 0.12f, 0.30f, 3.0f),
            new MeleeAttackStep("Backslash", 12f, 10f, 0.10f, 0.12f, 0.32f, 3.0f),
            new MeleeAttackStep("Spin Cleave", 20f, 16f, 0.16f, 0.16f, 0.48f, 4.5f),
        };
        [Tooltip("Only the ground attack is implemented in Milestone 1.")]
        [SerializeField] bool requireGrounded = true;

        [Header("Hit Detection")]
        [SerializeField] LayerMask hitMask = ~0;
        [Tooltip("Max colliders checked per frame. Increase if crowds of enemies are missed.")]
        [SerializeField, Range(4, 64)] int maxHitColliders = 16;
        [SerializeField] bool drawGizmos = true;

        Collider[] overlapBuffer;
        readonly List<IDamageable> hitThisSwing = new List<IDamageable>(8);

        int stepIndex = -1;
        float phaseTimer;
        bool nextQueued;

        public AttackPhase Phase { get; private set; } = AttackPhase.None;
        public int StepIndex => stepIndex;
        public bool IsAttacking => Phase != AttackPhase.None;
        public MeleeAttackStep CurrentStep => stepIndex >= 0 && stepIndex < combo.Length ? combo[stepIndex] : null;
        /// <summary>0..1 progress through the current phase.</summary>
        public float PhaseProgress { get; private set; }
        public int ComboLength => combo.Length;

        /// <summary>(stepIndex, step)</summary>
        public event Action<int, MeleeAttackStep> AttackStarted;
        public event Action<AttackPhase> PhaseChanged;
        /// <summary>Raised for every target damaged: (target, hit info).</summary>
        public event Action<IDamageable, DamageInfo> TargetHit;
        public event Action AttackEnded;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (stamina == null) stamina = GetComponent<StaminaComponent>();
            overlapBuffer = new Collider[maxHitColliders];

            if (combo == null || combo.Length == 0)
                Debug.LogError($"{nameof(PlayerMeleeCombat)} on '{name}' has no combo steps configured.", this);
            if (controller == null || actionState == null)
                Debug.LogError($"{nameof(PlayerMeleeCombat)} requires {nameof(ArlenPlayerController)} and {nameof(PlayerActionState)}.", this);
        }

        void OnEnable()
        {
            if (actionState != null) actionState.Interrupted += OnInterrupted;
        }

        void OnDisable()
        {
            if (actionState != null) actionState.Interrupted -= OnInterrupted;
            if (IsAttacking) EndAttack(endAction: true);
        }

        void Update()
        {
            if (controller == null || actionState == null || combo == null || combo.Length == 0) return;

            if (!IsAttacking)
            {
                TryStartCombo();
                return;
            }

            if (input != null && Phase != AttackPhase.Recovery && input.ConsumeAttack()) nextQueued = true;
            Tick(Time.deltaTime);
        }

        void TryStartCombo()
        {
            if (input == null) return;
            if (requireGrounded && !controller.IsGrounded) return;
            if (actionState.IsBusy) return; // keep the press buffered until free
            if (!input.ConsumeAttack()) return;

            if (!PayFor(combo[0])) return;
            if (!actionState.TryBegin(PlayerAction.Attack)) return;
            StartStep(0);
        }

        bool PayFor(MeleeAttackStep step) => stamina == null || stamina.TrySpend(step.staminaCost);

        void StartStep(int index)
        {
            stepIndex = index;
            nextQueued = false;
            hitThisSwing.Clear();
            actionState.SetCancelable(PlayerAction.Attack, false);

            // Let the player steer each swing toward their input direction.
            Vector3 aim = controller.GetCameraRelativeInput();
            if (aim.sqrMagnitude > 0.01f) controller.FaceDirection(aim);

            SetPhase(AttackPhase.Windup);
            AttackStarted?.Invoke(index, combo[index]);
        }

        void Tick(float dt)
        {
            var step = combo[stepIndex];
            phaseTimer += dt;

            switch (Phase)
            {
                case AttackPhase.Windup:
                    PhaseProgress = step.windup > 0f ? Mathf.Clamp01(phaseTimer / step.windup) : 1f;
                    controller.SetOverrideVelocity(transform.forward * step.lungeSpeed);
                    if (phaseTimer >= step.windup) SetPhase(AttackPhase.Active);
                    break;

                case AttackPhase.Active:
                    PhaseProgress = Mathf.Clamp01(phaseTimer / step.active);
                    controller.SetOverrideVelocity(transform.forward * step.lungeSpeed);
                    DetectHits(step);
                    if (phaseTimer >= step.active)
                    {
                        SetPhase(AttackPhase.Recovery);
                        actionState.SetCancelable(PlayerAction.Attack, true);
                    }
                    break;

                case AttackPhase.Recovery:
                    PhaseProgress = step.recovery > 0f ? Mathf.Clamp01(phaseTimer / step.recovery) : 1f;
                    controller.SetOverrideVelocity(Vector3.zero);

                    bool wantsNext = nextQueued || (input != null && input.ConsumeAttack());
                    if (wantsNext && stepIndex + 1 < combo.Length && PayFor(combo[stepIndex + 1]))
                    {
                        StartStep(stepIndex + 1);
                        return;
                    }
                    nextQueued = false;

                    if (phaseTimer >= step.recovery) EndAttack(endAction: true);
                    break;
            }
        }

        void SetPhase(AttackPhase phase)
        {
            Phase = phase;
            phaseTimer = 0f;
            PhaseProgress = 0f;
            PhaseChanged?.Invoke(phase);
        }

        void DetectHits(MeleeAttackStep step)
        {
            Vector3 center = transform.TransformPoint(step.hitOffset);
            int count = Physics.OverlapSphereNonAlloc(center, step.hitRadius, overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = overlapBuffer[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || hitThisSwing.Contains(target)) continue;
                hitThisSwing.Add(target);

                Vector3 dir = col.bounds.center - transform.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
                var info = new DamageInfo(step.damage, gameObject, col.ClosestPoint(center), dir);
                if (target.ApplyDamage(info) == DamageResult.Applied) TargetHit?.Invoke(target, info);
            }
        }

        void EndAttack(bool endAction)
        {
            Phase = AttackPhase.None;
            stepIndex = -1;
            phaseTimer = 0f;
            PhaseProgress = 0f;
            nextQueued = false;
            hitThisSwing.Clear();
            if (endAction && actionState != null) actionState.End(PlayerAction.Attack);
            PhaseChanged?.Invoke(AttackPhase.None);
            AttackEnded?.Invoke();
        }

        void OnInterrupted(PlayerAction action)
        {
            // Dodge-cancel or forced clear: the action state has already moved on.
            if (action == PlayerAction.Attack && IsAttacking) EndAttack(endAction: false);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || combo == null) return;
            var step = CurrentStep ?? (combo.Length > 0 ? combo[0] : null);
            if (step == null) return;
            Gizmos.color = Phase == AttackPhase.Active ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.TransformPoint(step.hitOffset), step.hitRadius);
        }
    }
}
