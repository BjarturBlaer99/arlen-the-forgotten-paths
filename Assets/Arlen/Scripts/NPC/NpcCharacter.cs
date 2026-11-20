using System;
using Arlen.DialogueSystem;
using Arlen.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Arlen.NPC
{
    /// <summary>
    /// A villager the player can talk to. Plays a first-meeting conversation once, then a repeat
    /// conversation (cycling through several if given). Turns to face the player when nearby.
    /// <see cref="onConversationFinished"/> is the hook quests will use in Milestone 4.
    /// </summary>
    public class NpcCharacter : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Villager";
        [SerializeField] DialogueData firstMeeting;
        [Tooltip("Played after the first meeting, cycling in order.")]
        [SerializeField] DialogueData[] repeatConversations;

        [Header("Presence")]
        [SerializeField, Min(0f)] float noticeRadius = 4.5f;
        [SerializeField, Min(0f)] float turnSpeed = 240f;
        [Tooltip("Head height used for interaction range checks.")]
        [SerializeField] float interactionHeight = 1.2f;

        [SerializeField] UnityEvent onConversationFinished;

        Quaternion restRotation;
        Transform lookTarget;
        int repeatIndex;
        bool talking;

        public string DisplayName => displayName;
        public bool HasMet { get; private set; }
        public bool IsTalking => talking;
        public int ConversationsHad { get; private set; }
        public event Action<NpcCharacter> ConversationFinished;

        public string InteractionPrompt => "Talk";
        public Vector3 InteractionPoint => transform.position + Vector3.up * interactionHeight;

        public void Configure(string npcName, DialogueData first, params DialogueData[] repeats)
        {
            displayName = npcName;
            firstMeeting = first;
            repeatConversations = repeats;
        }

        void Awake() => restRotation = transform.rotation;

        public bool CanInteract(PlayerInteractor interactor) => !talking && NextConversation() != null;

        DialogueData NextConversation()
        {
            if (!HasMet && firstMeeting != null) return firstMeeting;
            if (repeatConversations != null && repeatConversations.Length > 0)
                return repeatConversations[repeatIndex % repeatConversations.Length];
            return firstMeeting;
        }

        public void Interact(PlayerInteractor interactor)
        {
            var conversation = NextConversation();
            if (conversation == null || interactor == null || interactor.Dialogue == null) return;

            bool wasFirst = !HasMet && conversation == firstMeeting;
            lookTarget = interactor.transform;
            if (interactor.Dialogue.Play(conversation, displayName, () => OnFinished(wasFirst)))
                talking = true;
        }

        void OnFinished(bool wasFirst)
        {
            talking = false;
            if (wasFirst) HasMet = true;
            else repeatIndex++;
            ConversationsHad++;
            onConversationFinished?.Invoke();
            ConversationFinished?.Invoke(this);
        }

        void Update()
        {
            // Notice the player when close; otherwise drift back to the resting pose.
            Quaternion target = restRotation;
            Transform t = talking ? lookTarget : FindNearbyPlayer();
            if (t != null)
            {
                Vector3 to = t.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) target = Quaternion.LookRotation(to);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        Transform FindNearbyPlayer()
        {
            var all = Combat.Targetable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.Team != Combat.Team.Player) continue;
                Vector3 d = c.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= noticeRadius * noticeRadius) return c.transform;
            }
            return null;
        }
    }
}
