using System.Collections.Generic;
using UnityEngine;

namespace Arlen.Core
{
    /// <summary>A full-screen menu (inventory, chest) that Pause/Escape should close instead of pausing.</summary>
    public interface IModalScreen
    {
        void Close();
    }

    /// <summary>Tracks open modal screens so the pause button and cursor capture can defer to them.</summary>
    public static class ModalScreens
    {
        static readonly List<IModalScreen> Open = new List<IModalScreen>(2);

        public static bool AnyOpen => Open.Count > 0;

        public static void Register(IModalScreen screen)
        {
            if (screen != null && !Open.Contains(screen)) Open.Add(screen);
        }

        public static void Unregister(IModalScreen screen) => Open.Remove(screen);

        /// <summary>Closes the most recently opened screen. Returns false if none was open.</summary>
        public static bool TryCloseTop()
        {
            if (Open.Count == 0) return false;
            var top = Open[Open.Count - 1];
            Open.RemoveAt(Open.Count - 1);
            top.Close();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Open.Clear();
    }
}
