using Arlen.Player;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class PlayerActionStateTests
    {
        GameObject go;
        PlayerActionState state;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ActionStateTest");
            state = go.AddComponent<PlayerActionState>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void BeginsWhenIdle()
        {
            Assert.IsTrue(state.TryBegin(PlayerAction.Attack));
            Assert.AreEqual(PlayerAction.Attack, state.Current);
        }

        [Test]
        public void CannotInterruptNonCancelableAction()
        {
            state.TryBegin(PlayerAction.Attack);
            Assert.IsFalse(state.TryBegin(PlayerAction.Dodge));
            Assert.AreEqual(PlayerAction.Attack, state.Current);
        }

        [Test]
        public void CancelableActionIsInterruptedAndNotified()
        {
            PlayerAction interrupted = PlayerAction.None;
            state.Interrupted += a => interrupted = a;
            state.TryBegin(PlayerAction.Attack);
            state.SetCancelable(PlayerAction.Attack, true);

            Assert.IsTrue(state.TryBegin(PlayerAction.Dodge));
            Assert.AreEqual(PlayerAction.Dodge, state.Current);
            Assert.AreEqual(PlayerAction.Attack, interrupted);
            Assert.IsFalse(state.IsCancelable, "New action starts non-cancelable.");
        }

        [Test]
        public void EndOnlyClearsOwnAction()
        {
            state.TryBegin(PlayerAction.Dodge);
            state.End(PlayerAction.Attack);
            Assert.AreEqual(PlayerAction.Dodge, state.Current);
            state.End(PlayerAction.Dodge);
            Assert.AreEqual(PlayerAction.None, state.Current);
        }
    }
}
