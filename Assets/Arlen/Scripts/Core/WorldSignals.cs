using System;
using UnityEngine;

namespace Arlen.Core
{
    /// <summary>Things that happen in the world that quests (and later achievements, journals) care about.</summary>
    public enum SignalKind
    {
        Talked,
        Examined,
        Defeated,
        EnteredArea,
        Activated,
        Collected,
    }

    /// <summary>
    /// Tiny event hub between world objects and the quest system. NPCs, examinables, enemies, areas and
    /// Waybells raise a signal with their stable id; listeners (QuestManager) decide what it means.
    /// World objects never reference quests, so content can be rearranged freely.
    /// </summary>
    public static class WorldSignals
    {
        /// <summary>(kind, id, amount)</summary>
        public static event Action<SignalKind, string, int> Raised;

        public static void Raise(SignalKind kind, string id, int amount = 1)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            Raised?.Invoke(kind, id, amount);
        }

        // Static events survive play sessions when domain reload is disabled; start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Raised = null;
    }
}
