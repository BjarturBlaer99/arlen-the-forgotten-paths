using System;
using Arlen.Player;
using UnityEngine;

namespace Arlen.Combat
{
    public enum ParryPhase
    {
        None,
        /// <summary>Parry window open: parryable hits from the front are deflected.</summary>
        Active,
        /// <summary>Whiffed parry: brief vulnerable recovery, prevents spamming.</summary>
        Recovery,
    }

    /// <summary>
    /// Timed parry. Opens a short window during which parryable hits from the front are deflected
    /// (the health component reports <see cref="DamageResult.Parried"/> back to the attacker, which staggers).
    /// A successful parry ends the action immediately so the player can punish; a whiff leaves a recovery.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public class PlayerParry : MonoBehaviour, IDamageInterceptor
    {
        [Header("References (auto-resolved if empty)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] ArlenPlayerController controller;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] StaminaComponent stamina;
        [SerializeField] HealthComponent health;

        [Header("Timing (seconds)")]
        [Tooltip("How long the deflect window stays open after pressing parry.")]
        [SerializeField, Min(0.01f)] float parryWindow = 0.22f;
        [Tooltip("Lock-out after a parry that didn't catch anything.")]
        [SerializeField, Min(0f)] float whiffRecovery = 0.38f;
        [Tooltip("Lock-out after a successful parry (short, so the player can counter-attack).")]
        [SerializeField, Min(0f)] float successRecovery = 0.08f;

        [Header("Rules")]
        [SerializeField, Min(0f)] float staminaCost = 10f;
        [Tooltip("Hits must come from within this angle of the player's facing (degrees, total arc).")]
        [SerializeField, Range(0f, 360f)] float frontArc = 200f;

        float timer;
        float recoveryDuration;

        public ParryPhase Phase { get; private set; }
        public bool IsParrying => Phase != ParryPhase.None;
        public bool WindowOpen => Phase == ParryPhase.Active;

        public event Action Started;
        /// <summary>Raised on a successful deflect with the deflected hit.</summary>
        public event Action<DamageInfo> Parried;
        public event Action Ended;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (stamina == null) stamina = GetComponent<StaminaComponent>();
            if (health == null) health = GetComponent<HealthComponent>();
        }

        void OnEnable()
        {
            if (health != null) health.AddInterceptor(this);
            if (actionState != null) actionState.Interrupted += OnInterrupted;
        }

        void OnDisable()
        {
            if (health != null) health.RemoveInterceptor(this);
            if (actionState != null) actionState.Interrupted -= OnInterrupted;
            if (IsParrying) Stop(endAction: true);
        }

        void Update()
        {
            if (actionState == null || controller == null) return;

            if (!IsParrying)
            {
                TryStart();
                return;
            }

            timer += Time.deltaTime;
            controller.SetOverrideVelocity(Vector3.zero);

            if (Phase == ParryPhase.Active && timer >= parryWindow)
                EnterRecovery(whiffRecovery);
            else if (Phase == ParryPhase.Recovery && timer >= recoveryDuration)
                Stop(endAction: true);
        }

        void TryStart()
        {
            if (input == null || !controller.IsGrounded) return;
            if (actionState.IsBusy && !actionState.IsCancelable) return; // keep buffered
            if (!input.ConsumeParry()) return;
            if (stamina != null && !stamina.TrySpend(staminaCost)) return;
            if (!actionState.TryBegin(PlayerAction.Parry)) return;

            Vector3 aim = controller.GetCameraRelativeInput();
            if (aim.sqrMagnitude > 0.01f) controller.FaceDirection(aim);

            Phase = ParryPhase.Active;
            timer = 0f;
            Started?.Invoke();
        }

        public DamageResult Intercept(in DamageInfo info)
        {
            if (!WindowOpen || !info.Parryable) return DamageResult.Applied;

            // Must face the attack: hit direction travels toward us, so compare against our back.
            Vector3 toAttacker = -info.Direction;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, toAttacker) > frontArc * 0.5f)
                return DamageResult.Applied;

            Parried?.Invoke(info);
            EnterRecovery(successRecovery);
            return DamageResult.Parried;
        }

        void EnterRecovery(float duration)
        {
            Phase = ParryPhase.Recovery;
            timer = 0f;
            recoveryDuration = duration;
        }

        void Stop(bool endAction)
        {
            Phase = ParryPhase.None;
            timer = 0f;
            if (endAction && actionState != null) actionState.End(PlayerAction.Parry);
            Ended?.Invoke();
        }

        void OnInterrupted(PlayerAction action)
        {
            if (action == PlayerAction.Parry && IsParrying) Stop(endAction: false);
        }
    }
}
