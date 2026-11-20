using Arlen.Items;

namespace Arlen.Saving
{
    /// <summary>
    /// A scene component that writes its state into a <see cref="SaveData"/> and reads it back.
    /// <see cref="SaveManager"/> finds every ISaveable in the scene (inactive ones included), so new
    /// saveable things need no registration.
    /// </summary>
    public interface ISaveable
    {
        void CaptureState(SaveData data);
        void RestoreState(SaveData data, SaveContext context);
    }

    /// <summary>Lookups that restoring needs (item ids → assets).</summary>
    public sealed class SaveContext
    {
        public ItemDatabase Items { get; }

        public SaveContext(ItemDatabase items) => Items = items;

        public ItemDefinition ResolveItem(string id) => Items != null ? Items.Find(id) : null;
    }
}
