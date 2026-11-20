using Arlen.DialogueSystem;
using Arlen.Interaction;
using Arlen.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class InteractionScoringTests
    {
        [Test]
        public void ObjectInFrontBeatsObjectToTheSide()
        {
            float front = InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1.5f), 2.5f, 80f);
            float side = InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(1.4f, 0f, 0.6f), 2.5f, 80f);
            Assert.Less(front, side);
        }

        [Test]
        public void OutOfRangeOrBehindIsRejected()
        {
            Assert.AreEqual(float.MaxValue, InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 3f), 2.5f, 80f));
            Assert.AreEqual(float.MaxValue, InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1.5f), 2.5f, 80f));
        }

        [Test]
        public void HeightDifferenceIsIgnored()
        {
            float low = InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1f), 2.5f, 80f);
            float high = InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(0f, 1.8f, 1f), 2.5f, 80f);
            Assert.AreEqual(low, high, 0.0001f);
        }

        [Test]
        public void VeryCloseObjectsCountEvenIfNotFaced()
        {
            Assert.Less(InteractionScoring.Score(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -0.2f), 2.5f, 80f), float.MaxValue);
        }
    }

    public class DialogueTests
    {
        GameObject go;
        DialogueRunner runner;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DialogueTest");
            runner = go.AddComponent<DialogueRunner>(); // no input/view: logic only
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        static DialogueData Make(string speaker, params string[] texts)
        {
            var d = ScriptableObject.CreateInstance<DialogueData>();
            d.defaultSpeaker = speaker;
            d.lines = new DialogueData.Line[texts.Length];
            for (int i = 0; i < texts.Length; i++) d.lines[i] = new DialogueData.Line("", texts[i]);
            return d;
        }

        [Test]
        public void SpeakerResolution()
        {
            Assert.AreEqual("Mabel", DialogueData.ResolveSpeaker("", "Mabel"));
            Assert.AreEqual("You", DialogueData.ResolveSpeaker("You", "Mabel"));
            Assert.AreEqual("", DialogueData.ResolveSpeaker("-", "Mabel"), "Narration has no name.");
            Assert.AreEqual("", DialogueData.ResolveSpeaker("", "-"));
        }

        [Test]
        public void AdvanceRevealsLineThenMovesOnThenEnds()
        {
            var data = Make("Mabel", "Hello there.", "Bye.");
            bool done = false;
            Assert.IsTrue(runner.Play(data, null, () => done = true));
            Assert.IsTrue(runner.IsRunning);
            Assert.AreEqual("Mabel", runner.CurrentSpeaker);
            Assert.IsFalse(runner.LineFullyRevealed, "Typewriter starts empty.");

            runner.Advance(); // complete line 1
            Assert.IsTrue(runner.LineFullyRevealed);
            Assert.AreEqual(0, runner.LineIndex);

            runner.Advance(); // line 2
            Assert.AreEqual(1, runner.LineIndex);
            runner.Advance();
            runner.Advance(); // end
            Assert.IsFalse(runner.IsRunning);
            Assert.IsTrue(done);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void CannotStartWhileRunningAndEmptyDialogueIsRejected()
        {
            var data = Make("A", "One.");
            var empty = Make("B");
            Assert.IsFalse(runner.Play(empty));
            Assert.IsTrue(runner.Play(data));
            Assert.IsFalse(runner.Play(data), "Only one conversation at a time.");
            runner.Stop();
            Assert.IsFalse(runner.IsRunning);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(empty);
        }

        [Test]
        public void PlainTextLinesPlayAsNarration()
        {
            Assert.IsTrue(runner.Play(new[] { "A sign." }));
            Assert.AreEqual("", runner.CurrentSpeaker);
            Assert.AreEqual("A sign.", runner.CurrentText);
        }

        [Test]
        public void NpcPlaysFirstMeetingOnceThenCyclesRepeats()
        {
            var first = Make("", "Welcome back!");
            var repA = Make("", "Rolls are warm.");
            var repB = Make("", "No road there, dear.");
            var npcGo = new GameObject("NPC");
            var npc = npcGo.AddComponent<NpcCharacter>();
            npc.Configure("Mabel", first, repA, repB);
            var interactor = go.AddComponent<PlayerInteractor>();
            interactor.SetServices(runner, null);

            string Talk()
            {
                npc.Interact(interactor);
                string text = runner.CurrentText;
                Assert.AreEqual("Mabel", runner.CurrentSpeaker);
                while (runner.IsRunning) runner.Advance();
                return text;
            }

            Assert.AreEqual("Welcome back!", Talk());
            Assert.IsTrue(npc.HasMet);
            Assert.AreEqual("Rolls are warm.", Talk());
            Assert.AreEqual("No road there, dear.", Talk());
            Assert.AreEqual("Rolls are warm.", Talk(), "Repeats cycle.");
            Assert.AreEqual(4, npc.ConversationsHad);

            Object.DestroyImmediate(npcGo);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(repA);
            Object.DestroyImmediate(repB);
        }
    }
}
