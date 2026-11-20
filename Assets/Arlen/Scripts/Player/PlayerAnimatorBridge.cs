using Arlen.Combat;
using UnityEngine;

namespace Arlen.Player
{
    /// <summary>
    /// Integration point for production animation. Pushes gameplay state into an Animator using
    /// a fixed parameter contract, so a rigged Blender model only needs an Animator Controller with
    /// these parameters (missing ones are skipped, never errors):
    ///
    ///   float  Speed        planar speed (m/s)
    ///   float  SpeedNormalized  0 idle, 0.5 run, 1 sprint
    ///   bool   Grounded
    ///   float  VerticalSpeed
    ///   trigger Jump, Land, Dodge, Attack, Parry, ParrySuccess, Hit, Die
    ///   int    AttackIndex  combo step (0-based)
    ///   bool   Dead
    ///
    /// Gameplay never waits on animation; root motion is not used.
    /// </summary>
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] ArlenPlayerController controller;
        [SerializeField] PlayerDodge dodge;
        [SerializeField] PlayerMeleeCombat combat;
        [SerializeField] HealthComponent health;
        [SerializeField] PlayerParry parry;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int SpeedNormalizedId = Animator.StringToHash("SpeedNormalized");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int LandId = Animator.StringToHash("Land");
        static readonly int DodgeId = Animator.StringToHash("Dodge");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int AttackIndexId = Animator.StringToHash("AttackIndex");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DieId = Animator.StringToHash("Die");
        static readonly int DeadId = Animator.StringToHash("Dead");
        static readonly int ParryId = Animator.StringToHash("Parry");
        static readonly int ParrySuccessId = Animator.StringToHash("ParrySuccess");

        RuntimeAnimatorController cachedController;
        readonly System.Collections.Generic.HashSet<int> parameters = new System.Collections.Generic.HashSet<int>();

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
            if (dodge == null) dodge = GetComponent<PlayerDodge>();
            if (combat == null) combat = GetComponent<PlayerMeleeCombat>();
            if (health == null) health = GetComponent<HealthComponent>();
            if (parry == null) parry = GetComponent<PlayerParry>();
        }

        void OnEnable()
        {
            if (controller != null) { controller.Jumped += OnJumped; controller.Landed += OnLanded; }
            if (dodge != null) dodge.Started += OnDodge;
            if (combat != null) combat.AttackStarted += OnAttack;
            if (health != null) { health.Damaged += OnHit; health.Died += OnDied; health.Revived += OnRevived; }
            if (parry != null) { parry.Started += OnParry; parry.Parried += OnParrySuccess; }
        }

        void OnDisable()
        {
            if (controller != null) { controller.Jumped -= OnJumped; controller.Landed -= OnLanded; }
            if (dodge != null) dodge.Started -= OnDodge;
            if (combat != null) combat.AttackStarted -= OnAttack;
            if (health != null) { health.Damaged -= OnHit; health.Died -= OnDied; health.Revived -= OnRevived; }
            if (parry != null) { parry.Started -= OnParry; parry.Parried -= OnParrySuccess; }
        }

        bool Ready()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (cachedController != animator.runtimeAnimatorController)
            {
                cachedController = animator.runtimeAnimatorController;
                parameters.Clear();
                foreach (var p in animator.parameters) parameters.Add(p.nameHash);
            }
            return true;
        }

        void Update()
        {
            if (controller == null || !Ready()) return;
            float speed = controller.PlanarSpeed;
            SetFloat(SpeedId, speed);
            float norm = speed <= controller.RunSpeed
                ? 0.5f * speed / Mathf.Max(0.01f, controller.RunSpeed)
                : 0.5f + 0.5f * Mathf.InverseLerp(controller.RunSpeed, controller.SprintSpeed, speed);
            SetFloat(SpeedNormalizedId, norm);
            SetBool(GroundedId, controller.IsGrounded);
            SetFloat(VerticalSpeedId, controller.VerticalVelocity);
        }

        void OnJumped() => Trigger(JumpId);
        void OnLanded(float _) => Trigger(LandId);
        void OnDodge() => Trigger(DodgeId);
        void OnParry() => Trigger(ParryId);
        void OnParrySuccess(DamageInfo _) => Trigger(ParrySuccessId);
        void OnHit(DamageInfo _) => Trigger(HitId);
        void OnDied() { Trigger(DieId); SetBool(DeadId, true); }
        void OnRevived() => SetBool(DeadId, false);

        void OnAttack(int index, MeleeAttackStep _)
        {
            if (!Ready()) return;
            if (parameters.Contains(AttackIndexId)) animator.SetInteger(AttackIndexId, index);
            Trigger(AttackId);
        }

        void Trigger(int id) { if (Ready() && parameters.Contains(id)) animator.SetTrigger(id); }
        void SetFloat(int id, float v) { if (parameters.Contains(id)) animator.SetFloat(id, v); }
        void SetBool(int id, bool v) { if (Ready() && parameters.Contains(id)) animator.SetBool(id, v); }
    }
}
