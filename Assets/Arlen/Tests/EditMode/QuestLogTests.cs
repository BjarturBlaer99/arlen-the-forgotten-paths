using System.Collections.Generic;
using Arlen.Core;
using Arlen.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class QuestLogTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        QuestDefinition Quest(string id, params ObjectiveDefinition[] objectives)
        {
            var q = ScriptableObject.CreateInstance<QuestDefinition>();
            q.id = id;
            q.title = id;
            q.objectives = objectives;
            created.Add(q);
            return q;
        }

        static ObjectiveDefinition Obj(string id, SignalKind kind, int count = 1, params string[] targets) =>
            new ObjectiveDefinition { id = id, kind = kind, requiredCount = count, targetIds = targets };

        QuestDefinition RoadQuest() => Quest("road",
            Obj("ask", SignalKind.Talked, 1, "ask_about_road"),
            Obj("look", SignalKind.Examined, 1, "vanished_road"),
            Obj("fight", SignalKind.Defeated, 1, "boar"),
            Obj("ring", SignalKind.Activated, 1, "bell"));

        [Test]
        public void StartingAQuestMakesItTrackedWithFirstObjective()
        {
            var log = new QuestLog();
            QuestState started = null;
            log.QuestStarted += q => started = q;

            Assert.IsTrue(log.Start(RoadQuest()));
            Assert.AreSame(started, log.Tracked);
            Assert.AreEqual(QuestStatus.Active, log.GetStatus("road"));
            Assert.AreEqual("ask", log.Tracked.CurrentObjective.id);
            Assert.IsTrue(log.IsObjectiveActive("road", "ask"));
        }

        [Test]
        public void QuestCannotStartTwiceOrWithoutId()
        {
            var log = new QuestLog();
            var q = RoadQuest();
            Assert.IsTrue(log.Start(q));
            Assert.IsFalse(log.Start(q));
            Assert.IsFalse(log.Start(Quest("")));
            Assert.IsFalse(log.Start(null));
        }

        [Test]
        public void ObjectivesCompleteInOrderAndOnlyForMatchingSignals()
        {
            var log = new QuestLog();
            log.Start(RoadQuest());
            var completed = new List<int>();
            log.ObjectiveCompleted += (q, i) => completed.Add(i);

            log.HandleSignal(SignalKind.Defeated, "boar");      // not current yet
            log.HandleSignal(SignalKind.Talked, "someone_else"); // wrong target
            Assert.AreEqual("ask", log.Tracked.CurrentObjective.id);

            log.HandleSignal(SignalKind.Talked, "ask_about_road");
            Assert.AreEqual("look", log.Tracked.CurrentObjective.id);
            CollectionAssert.AreEqual(new[] { 0 }, completed);
        }

        [Test]
        public void EarlierFactsCompleteLaterObjectivesWhenTheyBecomeCurrent()
        {
            var log = new QuestLog();
            log.Start(RoadQuest());
            // Player wanders ahead: examines the road and beats the boar before asking around.
            log.HandleSignal(SignalKind.Examined, "vanished_road");
            log.HandleSignal(SignalKind.Defeated, "boar");
            Assert.AreEqual("ask", log.Tracked.CurrentObjective.id);

            log.HandleSignal(SignalKind.Talked, "ask_about_road");
            Assert.AreEqual("ring", log.Tracked.CurrentObjective.id, "Already-done objectives should be skipped.");
        }

        [Test]
        public void OneSignalNeverCompletesTwoConsecutiveObjectives()
        {
            var log = new QuestLog();
            log.Start(Quest("twice", Obj("a", SignalKind.Talked, 1, "mabel"), Obj("b", SignalKind.Talked, 1, "mabel")));
            log.HandleSignal(SignalKind.Talked, "mabel");
            Assert.AreEqual("b", log.Tracked.CurrentObjective.id);
            log.HandleSignal(SignalKind.Talked, "mabel");
            Assert.AreEqual(QuestStatus.Completed, log.GetStatus("twice"));
        }

        [Test]
        public void CountedObjectivesNeedTheFullCountAndReportProgress()
        {
            var log = new QuestLog();
            log.Start(Quest("herbs", Obj("collect", SignalKind.Collected, 3, "sunmoss")));
            int changes = 0;
            log.Changed += _ => changes++;

            log.HandleSignal(SignalKind.Collected, "sunmoss", 2);
            Assert.AreEqual(2, log.Tracked.Progress);
            Assert.AreEqual(QuestStatus.Active, log.GetStatus("herbs"));
            log.HandleSignal(SignalKind.Collected, "sunmoss");
            Assert.AreEqual(QuestStatus.Completed, log.GetStatus("herbs"));
            Assert.GreaterOrEqual(changes, 2);
        }

        [Test]
        public void AnyTargetObjectiveAcceptsAnyIdOfThatKind()
        {
            var log = new QuestLog();
            log.Start(Quest("chat", Obj("talk", SignalKind.Talked)));
            log.HandleSignal(SignalKind.Examined, "sign");
            Assert.AreEqual(QuestStatus.Active, log.GetStatus("chat"));
            log.HandleSignal(SignalKind.Talked, "bram");
            Assert.AreEqual(QuestStatus.Completed, log.GetStatus("chat"));
        }

        [Test]
        public void CompletingTheLastObjectiveCompletesTheQuestOnce()
        {
            var log = new QuestLog();
            log.Start(Quest("short", Obj("ring", SignalKind.Activated, 1, "bell")));
            int completions = 0;
            log.QuestCompleted += _ => completions++;

            log.HandleSignal(SignalKind.Activated, "bell");
            log.HandleSignal(SignalKind.Activated, "bell");
            Assert.AreEqual(1, completions);
            Assert.IsNull(log.Tracked);
            Assert.IsNull(log.Get("short").CurrentObjective);
        }

        [Test]
        public void CaptureAndRestoreRoundTripsProgressAndFacts()
        {
            var quest = RoadQuest();
            var log = new QuestLog();
            log.Start(quest);
            log.HandleSignal(SignalKind.Talked, "ask_about_road");
            log.HandleSignal(SignalKind.Activated, "bell"); // fact for later

            var entries = log.Capture();
            var facts = new List<string>(log.Facts);

            var restored = new QuestLog();
            restored.Restore(entries, facts, id => id == "road" ? quest : null);
            Assert.AreEqual("look", restored.Tracked.CurrentObjective.id);
            Assert.IsTrue(restored.HasFact(SignalKind.Activated, "bell"));

            restored.HandleSignal(SignalKind.Examined, "vanished_road");
            restored.HandleSignal(SignalKind.Defeated, "boar");
            Assert.AreEqual(QuestStatus.Completed, restored.GetStatus("road"), "Ringing the bell earlier should count after loading.");
        }

        [Test]
        public void RestoreSkipsUnknownQuestsAndClampsShortenedOnes()
        {
            var shortQuest = Quest("road", Obj("ask", SignalKind.Talked, 1, "x"));
            var log = new QuestLog();
            log.Restore(new[]
            {
                new QuestSaveEntry { questId = "removed", status = QuestStatus.Active },
                new QuestSaveEntry { questId = "road", status = QuestStatus.Active, objectiveIndex = 3 },
            }, null, id => id == "road" ? shortQuest : null);

            Assert.AreEqual(1, log.Quests.Count);
            Assert.AreEqual(QuestStatus.Completed, log.GetStatus("road"));
        }

        [Test]
        public void QuestConditionsReadTheLog()
        {
            var log = new QuestLog();
            var query = new LogQuery(log);
            var objective = new QuestCondition(QuestConditionKind.ObjectiveActive, "road", "ask");
            var notStarted = new QuestCondition(QuestConditionKind.QuestNotStarted, "road");
            Assert.IsFalse(objective.IsMet(query));
            Assert.IsTrue(notStarted.IsMet(query));

            log.Start(RoadQuest());
            Assert.IsTrue(objective.IsMet(query));
            Assert.IsTrue(new QuestCondition(QuestConditionKind.QuestActive, "road").IsMet(query));
            Assert.IsFalse(objective.IsMet(null));
        }

        sealed class LogQuery : IQuestQuery
        {
            readonly QuestLog log;
            public LogQuery(QuestLog log) => this.log = log;
            public QuestStatus GetStatus(string questId) => log.GetStatus(questId);
            public bool IsObjectiveActive(string questId, string objectiveId) => log.IsObjectiveActive(questId, objectiveId);
        }
    }
}
