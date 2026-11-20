using Arlen.Items;
using Arlen.Quests;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.UI
{
    /// <summary>Turns gameplay events (quests, items, saving) into toast messages. The systems know nothing of the UI.</summary>
    public class HudNotifications : MonoBehaviour
    {
        [SerializeField] ToastFeed toasts;
        [SerializeField] QuestManager quests;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] SaveManager saves;

        [SerializeField] Color questColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] Color itemColor = Color.white;
        [SerializeField] Color systemColor = new Color(0.65f, 0.95f, 0.9f);

        public void Configure(ToastFeed feed, QuestManager questManager, PlayerInventory pack, SaveManager saveManager)
        {
            toasts = feed;
            quests = questManager;
            inventory = pack;
            saves = saveManager;
        }

        void OnEnable()
        {
            if (quests != null)
            {
                quests.Log.QuestStarted += OnQuestStarted;
                quests.Log.ObjectiveCompleted += OnObjectiveCompleted;
                quests.Log.QuestCompleted += OnQuestCompleted;
            }
            if (inventory != null)
            {
                inventory.ItemReceived += OnItemReceived;
                inventory.ItemUsed += OnItemUsed;
            }
            if (saves != null) saves.Saved += OnSaved;
        }

        void OnDisable()
        {
            if (quests != null)
            {
                quests.Log.QuestStarted -= OnQuestStarted;
                quests.Log.ObjectiveCompleted -= OnObjectiveCompleted;
                quests.Log.QuestCompleted -= OnQuestCompleted;
            }
            if (inventory != null)
            {
                inventory.ItemReceived -= OnItemReceived;
                inventory.ItemUsed -= OnItemUsed;
            }
            if (saves != null) saves.Saved -= OnSaved;
        }

        void Show(string message, Color color)
        {
            if (toasts != null) toasts.Show(message, color);
        }

        void OnQuestStarted(QuestState q) => Show($"New quest: {q.Definition.title}", questColor);

        void OnObjectiveCompleted(QuestState q, int index)
        {
            // The last objective is announced by the quest completion instead.
            if (index < q.Definition.ObjectiveCount - 1) Show("Objective complete", questColor);
        }

        void OnQuestCompleted(QuestState q)
        {
            Show($"Quest complete: {q.Definition.title}", questColor);
            if (!string.IsNullOrEmpty(q.Definition.completionText)) Show(q.Definition.completionText, questColor);
        }

        void OnItemReceived(ItemDefinition item, int count) =>
            Show(count > 1 ? $"+ {item.displayName} ×{count}" : $"+ {item.displayName}", itemColor);

        void OnItemUsed(ItemDefinition item) => Show($"Used {item.displayName}", itemColor);

        void OnSaved() => Show("Progress saved", systemColor);
    }
}
