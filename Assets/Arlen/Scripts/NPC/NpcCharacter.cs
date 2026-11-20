using System;
using System.Collections.Generic;
using Arlen.Core;
using Arlen.DialogueSystem;
using Arlen.Interaction;
using Arlen.Items;
using Arlen.Quests;
using Arlen.Saving;
using UnityEngine;
using UnityEngine.Events;

namespace Arlen.NPC
{
    /// <summary>
    /// A villager the player can talk to. Plays a first-meeting conversation once, then a repeat
    /// conversation (cycling through several if given). Quest conversations take priority over repeats
    /// while their condition holds; one that becomes available during the first meeting follows straight
    /// on from it. Every finished conversation raises a <see cref="SignalKind.Talked"/> signal with the NPC's id.
    /// Turns to face the player when nearby.
    /// </summary>
    public class NpcCharacter : MonoBehaviour, IInteractable, ISaveable
    {
        [Serializable]
        public struct QuestConversation
        {
            public QuestCondition condition;
            public DialogueData dialogue;
            [Tooltip("Extra Talked signal raised when this conversation finishes (e.g. \"ask_about_road\").")]
            public string talkSignal;
            [Tooltip("Play only once.")]
            public bool once;
        }

        [Tooltip("Stable id for quests and saves. Defaults to the display name.")]
        [SerializeField] string npcId;
        [SerializeField] string displayName = "Villager";
        [SerializeField] DialogueData firstMeeting;
        [Tooltip("Played after the first meeting, cycling in order.")]
        [SerializeField] DialogueData[] repeatConversations;
        [Tooltip("Checked in order before repeat conversations.")]
        [SerializeField] QuestConversation[] questConversations = Array.Empty<QuestConversation>();
        [Tooltip("Given to the player when the first meeting ends.")]
        [SerializeField] ItemAmount firstMeetingGift;

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
        readonly HashSet<int> playedQuestConversations = new HashSet<int>();

        public string Id => string.IsNullOrEmpty(npcId) ? displayName : npcId;
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

        public void ConfigureQuestContent(QuestConversation[] conversations, ItemAmount gift)
        {
            questConversations = conversations ?? Array.Empty<QuestConversation>();
            firstMeetingGift = gift;
        }

        void Awake() => restRotation = transform.rotation;

        public bool CanInteract(PlayerInteractor interactor) => !talking && NextConversation(interactor?.Quests, out _) != null;

        /// <summary>Index into questConversations whose condition holds, or -1.</summary>
        int FindQuestConversation(IQuestQuery quests)
        {
            if (quests == null || questConversations == null) return -1;
            for (int i = 0; i < questConversations.Length; i++)
            {
                var qc = questConversations[i];
                if (qc.dialogue == null || (qc.once && playedQuestConversations.Contains(i))) continue;
                if (qc.condition.IsMet(quests)) return i;
            }
            return -1;
        }

        DialogueData NextConversation(IQuestQuery quests, out int questIndex)
        {
            questIndex = -1;
            if (!HasMet && firstMeeting != null) return firstMeeting;
            questIndex = FindQuestConversation(quests);
            if (questIndex >= 0) return questConversations[questIndex].dialogue;
            if (repeatConversations != null && repeatConversations.Length > 0)
                return repeatConversations[repeatIndex % repeatConversations.Length];
            return firstMeeting;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor == null || interactor.Dialogue == null) return;
            lookTarget = interactor.transform;
            Play(interactor, NextConversation(interactor.Quests, out int questIndex), questIndex);
        }

        void Play(PlayerInteractor interactor, DialogueData conversation, int questIndex)
        {
            if (conversation == null) return;
            bool wasFirst = !HasMet && conversation == firstMeeting;
            if (interactor.Dialogue.Play(conversation, displayName, () => OnFinished(interactor, wasFirst, questIndex)))
                talking = true;
        }

        void OnFinished(PlayerInteractor interactor, bool wasFirst, int questIndex)
        {
            talking = false;
            if (wasFirst)
            {
                HasMet = true;
                GiveGift(interactor);
            }
            else if (questIndex >= 0) playedQuestConversations.Add(questIndex);
            else repeatIndex++;
            ConversationsHad++;

            onConversationFinished?.Invoke();
            ConversationFinished?.Invoke(this);
            WorldSignals.Raise(SignalKind.Talked, Id);
            if (questIndex >= 0) WorldSignals.Raise(SignalKind.Talked, questConversations[questIndex].talkSignal);

            // A quest topic that is relevant right now follows straight on from the greeting.
            if (wasFirst && interactor != null && interactor.Dialogue != null)
            {
                int next = FindQuestConversation(interactor.Quests);
                if (next >= 0) Play(interactor, questConversations[next].dialogue, next);
            }
        }

        void GiveGift(PlayerInteractor interactor)
        {
            if (firstMeetingGift.item == null || interactor == null) return;
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory != null) inventory.Receive(firstMeetingGift.item, Mathf.Max(1, firstMeetingGift.count));
        }

        public void CaptureState(SaveData data)
        {
            data.npcs.Add(new NpcSaveData
            {
                id = Id,
                hasMet = HasMet,
                repeatIndex = repeatIndex,
                conversationsHad = ConversationsHad,
                playedQuestConversations = new List<int>(playedQuestConversations),
            });
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            var saved = data.FindNpc(Id);
            if (saved == null) return;
            HasMet = saved.hasMet;
            repeatIndex = saved.repeatIndex;
            ConversationsHad = saved.conversationsHad;
            playedQuestConversations.Clear();
            if (saved.playedQuestConversations != null)
                foreach (int i in saved.playedQuestConversations) playedQuestConversations.Add(i);
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
