using System;
using System.Collections;
using System.Collections.Generic;
using Arlen.Core;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.Quests
{
    /// <summary>
    /// Scene service that owns the <see cref="QuestLog"/>, feeds it <see cref="WorldSignals"/>, starts
    /// opening quests and saves/restores progress. UI listens to <see cref="Log"/> events; nothing here
    /// knows about the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestManager : MonoBehaviour, IQuestQuery, ISaveable
    {
        [Tooltip("Every quest that can appear in this game (used to resolve saved quest ids).")]
        [SerializeField] QuestDefinition[] catalog = Array.Empty<QuestDefinition>();
        [Tooltip("Started automatically on a new game (ignored when the save already has them).")]
        [SerializeField] QuestDefinition[] startingQuests = Array.Empty<QuestDefinition>();
        [SerializeField, Min(0f)] float startDelay = 2.5f;

        QuestLog log;

        public QuestLog Log => log ??= new QuestLog();
        public IReadOnlyList<QuestDefinition> Catalog => catalog;

        public void Configure(QuestDefinition[] questCatalog, QuestDefinition[] autoStart, float delay = 0f)
        {
            catalog = questCatalog ?? Array.Empty<QuestDefinition>();
            startingQuests = autoStart ?? Array.Empty<QuestDefinition>();
            startDelay = delay;
        }

        void OnEnable() => WorldSignals.Raised += OnSignal;
        void OnDisable() => WorldSignals.Raised -= OnSignal;

        IEnumerator Start()
        {
            // Saves are restored in SaveManager.Start (earlier execution order), so quests already in the
            // save are no longer NotStarted and are skipped here.
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            foreach (var q in startingQuests)
                if (q != null) Log.Start(q);
        }

        void OnSignal(SignalKind kind, string id, int amount) => Log.HandleSignal(kind, id, amount);

        public bool StartQuest(QuestDefinition quest) => Log.Start(quest);

        public QuestStatus GetStatus(string questId) => Log.GetStatus(questId);
        public bool IsObjectiveActive(string questId, string objectiveId) => Log.IsObjectiveActive(questId, objectiveId);

        QuestDefinition Resolve(string questId)
        {
            foreach (var q in catalog)
                if (q != null && q.id == questId) return q;
            foreach (var q in startingQuests)
                if (q != null && q.id == questId) return q;
            return null;
        }

        public void CaptureState(SaveData data)
        {
            data.quests = Log.Capture();
            data.questFacts = new List<string>(Log.Facts);
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            Log.Restore(data.quests, data.questFacts, Resolve);
        }
    }
}
