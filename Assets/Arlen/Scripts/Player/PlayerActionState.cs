using System;
using UnityEngine;

namespace Arlen.Player
{
    public enum PlayerAction
    {
        None,
        Dodge,
        Attack,
    }

    /// <summary>
    /// Arbitrates which committed action (dodge, attack, later parry/abilities) currently owns the character.
    /// Keeps action components decoupled: they only ask this component for permission and listen for interrupts.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerActionState : MonoBehaviour
    {
        public PlayerAction Current { get; private set; } = PlayerAction.None;
        /// <summary>True when the current action may be interrupted by a new one (e.g. attack recovery → dodge).</summary>
        public bool IsCancelable { get; private set; }
        public bool IsBusy => Current != PlayerAction.None;

        /// <summary>Raised with the action that was cut short so its owner can clean up.</summary>
        public event Action<PlayerAction> Interrupted;
        public event Action<PlayerAction> Began;
        public event Action<PlayerAction> Ended;

        /// <summary>Starts an action if the character is free, or if the current action is cancelable.</summary>
        public bool TryBegin(PlayerAction action)
        {
            if (action == PlayerAction.None) return false;

            if (Current == PlayerAction.None)
            {
                Set(action);
                return true;
            }

            if (IsCancelable && Current != action)
            {
                var interrupted = Current;
                Set(action);
                Interrupted?.Invoke(interrupted);
                return true;
            }

            return false;
        }

        public void SetCancelable(PlayerAction owner, bool cancelable)
        {
            if (Current == owner) IsCancelable = cancelable;
        }

        public void End(PlayerAction owner)
        {
            if (Current != owner || owner == PlayerAction.None) return;
            Current = PlayerAction.None;
            IsCancelable = false;
            Ended?.Invoke(owner);
        }

        /// <summary>Forcefully clears all actions (death, respawn, cutscenes).</summary>
        public void ForceClear()
        {
            if (Current == PlayerAction.None) return;
            var interrupted = Current;
            Current = PlayerAction.None;
            IsCancelable = false;
            Interrupted?.Invoke(interrupted);
        }

        void Set(PlayerAction action)
        {
            Current = action;
            IsCancelable = false;
            Began?.Invoke(action);
        }
    }
}
