using System;
using Arlen.Combat;
using Arlen.DialogueSystem;
using Arlen.Player;
using Arlen.Quests;
using Arlen.UI;
using UnityEngine;

namespace Arlen.Interaction
{
    /// <summary>
    /// Finds the best interactable near the player (distance + facing, which is more reliable than a
    /// camera ray in third person), raises focus events for the prompt UI and triggers it on Interact.
    /// Also gives interactables access to player-side services (dialogue, screen fade, health) without
    /// them having to search the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References (auto-resolved where possible)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] ArlenPlayerController controller;
        [Tooltip("Scene service used by NPCs and examinable objects.")]
        [SerializeField] DialogueRunner dialogue;
        [SerializeField] ScreenFader screenFader;
        [Tooltip("Lets NPCs pick quest-aware conversations.")]
        [SerializeField] QuestManager quests;
        [Tooltip("Used by chests to show their contents.")]
        [SerializeField] InventoryScreen inventoryScreen;

        [Header("Detection")]
        [SerializeField, Min(0.1f)] float radius = 2.4f;
        [SerializeField, Range(0f, 180f)] float maxAngle = 80f;
        [SerializeField] LayerMask mask = ~0;
        [SerializeField] Vector3 originOffset = new Vector3(0f, 1f, 0f);

        readonly Collider[] buffer = new Collider[24];

        public IInteractable Focus { get; private set; }
        public DialogueRunner Dialogue => dialogue;
        public ScreenFader ScreenFader => screenFader;
        public IQuestQuery Quests => quests;
        public InventoryScreen InventoryScreen => inventoryScreen;
        public HealthComponent Health { get; private set; }
        public StaminaComponent Stamina { get; private set; }

        /// <summary>Raised when the focused interactable changes (null when nothing is in reach).</summary>
        public event Action<IInteractable> FocusChanged;
        public event Action<IInteractable> Interacted;

        public void SetServices(DialogueRunner dialogueRunner, ScreenFader fader)
        {
            dialogue = dialogueRunner;
            screenFader = fader;
        }

        public void SetGameServices(QuestManager questManager, InventoryScreen screen)
        {
            quests = questManager;
            inventoryScreen = screen;
        }

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
            Health = GetComponent<HealthComponent>();
            Stamina = GetComponent<StaminaComponent>();
        }

        void OnEnable()
        {
            if (input != null) input.InteractPressed += OnInteractPressed;
        }

        void OnDisable()
        {
            if (input != null) input.InteractPressed -= OnInteractPressed;
            SetFocus(null);
        }

        bool CanInteractNow =>
            (input == null || !input.GameplayBlocked) &&
            (actionState == null || !actionState.IsBusy) &&
            (controller == null || controller.IsGrounded) &&
            (dialogue == null || !dialogue.IsRunning) &&
            (Health == null || Health.IsAlive);

        void Update()
        {
            SetFocus(CanInteractNow ? FindBest() : null);
        }

        IInteractable FindBest()
        {
            Vector3 origin = transform.position + originOffset;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, buffer, mask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = buffer[i].GetComponentInParent<IInteractable>();
                if (candidate == null || ReferenceEquals(candidate, best)) continue;
                float score = InteractionScoring.Score(transform.position, transform.forward, candidate.InteractionPoint, radius, maxAngle);
                if (score >= bestScore || !candidate.CanInteract(this)) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        void SetFocus(IInteractable value)
        {
            if (ReferenceEquals(Focus, value)) return;
            Focus = value;
            FocusChanged?.Invoke(value);
        }

        void OnInteractPressed()
        {
            if (!CanInteractNow) return;
            var target = Focus ?? FindBest();
            if (target == null || !target.CanInteract(this)) return;

            // Face what we're using.
            if (controller != null) controller.FaceDirection(target.InteractionPoint - transform.position);
            target.Interact(this);
            Interacted?.Invoke(target);
            SetFocus(null); // re-evaluated next frame (prompt text may have changed, e.g. Open → Close)
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + originOffset, radius);
        }
    }
}
