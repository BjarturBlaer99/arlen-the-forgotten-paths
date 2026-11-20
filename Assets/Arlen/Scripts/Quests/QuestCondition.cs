using System;

namespace Arlen.Quests
{
    /// <summary>Read-only view of quest progress, used by dialogue and world objects to react to the story.</summary>
    public interface IQuestQuery
    {
        QuestStatus GetStatus(string questId);
        bool IsObjectiveActive(string questId, string objectiveId);
    }

    public enum QuestConditionKind
    {
        ObjectiveActive,
        QuestActive,
        QuestCompleted,
        QuestNotStarted,
    }

    /// <summary>A simple serializable check such as "objective ask_about_road of road_that_vanished is current".</summary>
    [Serializable]
    public struct QuestCondition
    {
        public QuestConditionKind kind;
        public string questId;
        [UnityEngine.Tooltip("Only used by ObjectiveActive.")]
        public string objectiveId;

        public QuestCondition(QuestConditionKind kind, string questId, string objectiveId = null)
        {
            this.kind = kind;
            this.questId = questId;
            this.objectiveId = objectiveId;
        }

        public bool IsMet(IQuestQuery quests)
        {
            if (quests == null || string.IsNullOrEmpty(questId)) return false;
            switch (kind)
            {
                case QuestConditionKind.ObjectiveActive: return quests.IsObjectiveActive(questId, objectiveId);
                case QuestConditionKind.QuestActive: return quests.GetStatus(questId) == QuestStatus.Active;
                case QuestConditionKind.QuestCompleted: return quests.GetStatus(questId) == QuestStatus.Completed;
                case QuestConditionKind.QuestNotStarted: return quests.GetStatus(questId) == QuestStatus.NotStarted;
                default: return false;
            }
        }
    }
}
