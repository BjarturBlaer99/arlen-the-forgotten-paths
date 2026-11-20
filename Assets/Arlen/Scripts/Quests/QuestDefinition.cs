using System;
using Arlen.Core;
using UnityEngine;

namespace Arlen.Quests
{
    /// <summary>
    /// One step of a quest: "do <see cref="kind"/> to one of <see cref="targetIds"/>, <see cref="requiredCount"/> times".
    /// Target ids are the stable ids on world objects (NPC id, examinable id, enemy stats id, area id, Waybell id).
    /// </summary>
    [Serializable]
    public class ObjectiveDefinition
    {
        [Tooltip("Stable id, referenced by dialogue conditions and save files.")]
        public string id;
        [TextArea(1, 3)] public string description;
        public SignalKind kind = SignalKind.Talked;
        [Tooltip("Accepted target ids. Empty = any target of this kind.")]
        public string[] targetIds = Array.Empty<string>();
        [Min(1)] public int requiredCount = 1;

        public bool Accepts(SignalKind signalKind, string targetId)
        {
            if (signalKind != kind) return false;
            if (targetIds == null || targetIds.Length == 0) return true;
            return Array.IndexOf(targetIds, targetId) >= 0;
        }
    }

    /// <summary>
    /// A quest authored as an asset (Create ▸ Arlen ▸ Quest). Objectives are completed in order; only the
    /// current one listens for signals. Branching and optional objectives can be added when content needs them.
    /// </summary>
    [CreateAssetMenu(menuName = "Arlen/Quest", fileName = "Quest")]
    public class QuestDefinition : ScriptableObject
    {
        public string id;
        public string title = "Untitled Quest";
        [TextArea(2, 5)] public string description;
        public ObjectiveDefinition[] objectives = Array.Empty<ObjectiveDefinition>();
        [TextArea(1, 3)] [Tooltip("Shown when the quest completes.")]
        public string completionText;

        public int ObjectiveCount => objectives?.Length ?? 0;

        public int IndexOfObjective(string objectiveId)
        {
            if (objectives == null) return -1;
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].id == objectiveId) return i;
            return -1;
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) Debug.LogWarning($"Quest '{name}' has no id; it can't be saved or referenced.", this);
        }
    }
}
