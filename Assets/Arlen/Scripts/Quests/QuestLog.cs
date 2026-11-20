using System;
using System.Collections.Generic;
using Arlen.Core;

namespace Arlen.Quests
{
    public enum QuestStatus
    {
        NotStarted,
        Active,
        Completed,
    }

    /// <summary>Runtime progress of one quest.</summary>
    public sealed class QuestState
    {
        public QuestDefinition Definition { get; }
        public QuestStatus Status { get; internal set; }
        public int ObjectiveIndex { get; internal set; }
        /// <summary>Progress towards the current objective's required count.</summary>
        public int Progress { get; internal set; }

        public string Id => Definition.id;

        public ObjectiveDefinition CurrentObjective =>
            Status == QuestStatus.Active && ObjectiveIndex >= 0 && ObjectiveIndex < Definition.ObjectiveCount
                ? Definition.objectives[ObjectiveIndex]
                : null;

        internal QuestState(QuestDefinition definition) => Definition = definition;
    }

    /// <summary>Plain-data save form of a quest's progress.</summary>
    [Serializable]
    public class QuestSaveEntry
    {
        public string questId;
        public QuestStatus status;
        public int objectiveIndex;
        public int progress;
    }

    /// <summary>
    /// Engine-independent quest rules (wrapped by <see cref="QuestManager"/>). Objectives complete in order.
    /// Every signal is also remembered as a "fact", so an objective whose single-step goal was already
    /// done before it became current (examining the road early, ringing the bell first) completes at once
    /// instead of soft-locking the quest.
    /// </summary>
    public sealed class QuestLog
    {
        readonly List<QuestState> quests = new List<QuestState>();
        readonly HashSet<string> facts = new HashSet<string>();

        public IReadOnlyList<QuestState> Quests => quests;
        public IReadOnlyCollection<string> Facts => facts;

        public event Action<QuestState> QuestStarted;
        /// <summary>(quest, index of the objective that was just completed)</summary>
        public event Action<QuestState, int> ObjectiveCompleted;
        public event Action<QuestState> QuestCompleted;
        /// <summary>Anything about a quest changed (start, progress, objective, completion, restore).</summary>
        public event Action<QuestState> Changed;

        public static string FactKey(SignalKind kind, string id) => $"{kind}:{id}";

        public QuestState Get(string questId)
        {
            for (int i = 0; i < quests.Count; i++)
                if (quests[i].Id == questId) return quests[i];
            return null;
        }

        public QuestStatus GetStatus(string questId) => Get(questId)?.Status ?? QuestStatus.NotStarted;

        /// <summary>The quest shown on the HUD: the earliest-started active quest.</summary>
        public QuestState Tracked
        {
            get
            {
                for (int i = 0; i < quests.Count; i++)
                    if (quests[i].Status == QuestStatus.Active) return quests[i];
                return null;
            }
        }

        public bool IsObjectiveActive(string questId, string objectiveId)
        {
            var q = Get(questId);
            return q?.CurrentObjective != null && q.CurrentObjective.id == objectiveId;
        }

        public bool HasFact(SignalKind kind, string id) => facts.Contains(FactKey(kind, id));

        /// <summary>Starts a quest. Returns false if it is invalid or already started/completed.</summary>
        public bool Start(QuestDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return false;
            var state = Get(definition.id);
            if (state != null && state.Status != QuestStatus.NotStarted) return false;
            if (state == null)
            {
                state = new QuestState(definition);
                quests.Add(state);
            }

            state.Status = QuestStatus.Active;
            state.ObjectiveIndex = 0;
            state.Progress = 0;
            QuestStarted?.Invoke(state);
            Changed?.Invoke(state);
            SettleFromFacts(state);
            return true;
        }

        /// <summary>Feeds a world signal to every active quest.</summary>
        public void HandleSignal(SignalKind kind, string id, int amount = 1)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;

            // Snapshot count: completing a quest never adds quests, but stay safe against listeners that do.
            // The fact is recorded afterwards so one signal can't complete two consecutive objectives.
            int count = quests.Count;
            for (int i = 0; i < count; i++)
            {
                var q = quests[i];
                var objective = q.CurrentObjective;
                if (objective == null || !objective.Accepts(kind, id)) continue;
                q.Progress += amount;
                if (q.Progress >= objective.requiredCount) Advance(q);
                else Changed?.Invoke(q);
            }
            facts.Add(FactKey(kind, id));
        }

        void Advance(QuestState q)
        {
            int completedIndex = q.ObjectiveIndex;
            q.ObjectiveIndex++;
            q.Progress = 0;
            ObjectiveCompleted?.Invoke(q, completedIndex);

            if (q.ObjectiveIndex >= q.Definition.ObjectiveCount)
            {
                q.Status = QuestStatus.Completed;
                Changed?.Invoke(q);
                QuestCompleted?.Invoke(q);
                return;
            }

            Changed?.Invoke(q);
            SettleFromFacts(q);
        }

        /// <summary>Completes the current objective if its (single-step) goal is already a known fact.</summary>
        void SettleFromFacts(QuestState q)
        {
            var objective = q.CurrentObjective;
            if (objective == null || objective.requiredCount != 1) return;

            if (objective.targetIds == null || objective.targetIds.Length == 0)
            {
                string prefix = objective.kind + ":";
                foreach (var f in facts)
                    if (f.StartsWith(prefix, StringComparison.Ordinal)) { Advance(q); return; }
                return;
            }

            foreach (var target in objective.targetIds)
                if (facts.Contains(FactKey(objective.kind, target))) { Advance(q); return; }
        }

        // ───────────── Saving ─────────────

        public List<QuestSaveEntry> Capture()
        {
            var list = new List<QuestSaveEntry>(quests.Count);
            foreach (var q in quests)
                list.Add(new QuestSaveEntry { questId = q.Id, status = q.Status, objectiveIndex = q.ObjectiveIndex, progress = q.Progress });
            return list;
        }

        /// <summary>Replaces all progress. Unknown quest ids are skipped (content removed since the save).</summary>
        public void Restore(IEnumerable<QuestSaveEntry> entries, IEnumerable<string> savedFacts, Func<string, QuestDefinition> resolve)
        {
            quests.Clear();
            facts.Clear();
            if (savedFacts != null)
                foreach (var f in savedFacts) facts.Add(f);
            if (entries == null || resolve == null) return;

            foreach (var e in entries)
            {
                var def = resolve(e.questId);
                if (def == null) continue;
                var state = new QuestState(def)
                {
                    Status = e.status,
                    ObjectiveIndex = Math.Max(0, Math.Min(e.objectiveIndex, def.ObjectiveCount)),
                    Progress = Math.Max(0, e.progress),
                };
                // Content may have changed length since the save.
                if (state.Status == QuestStatus.Active && state.ObjectiveIndex >= def.ObjectiveCount) state.Status = QuestStatus.Completed;
                quests.Add(state);
            }
            foreach (var q in quests) Changed?.Invoke(q);
        }
    }
}
