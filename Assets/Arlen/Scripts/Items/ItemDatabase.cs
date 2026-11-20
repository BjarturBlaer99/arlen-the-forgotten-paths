using System.Collections.Generic;
using UnityEngine;

namespace Arlen.Items
{
    /// <summary>Every item in the game, so saves can turn item ids back into assets. Kept in sync by the editor builders.</summary>
    [CreateAssetMenu(menuName = "Arlen/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] List<ItemDefinition> items = new List<ItemDefinition>();

        Dictionary<string, ItemDefinition> lookup;

        public IReadOnlyList<ItemDefinition> All => items;

        public void SetItems(IEnumerable<ItemDefinition> definitions)
        {
            items = new List<ItemDefinition>(definitions);
            lookup = null;
        }

        public ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, ItemDefinition>();
                foreach (var item in items)
                    if (item != null && !string.IsNullOrEmpty(item.id)) lookup[item.id] = item;
            }
            return lookup.TryGetValue(id, out var found) ? found : null;
        }

        void OnValidate() => lookup = null;
    }
}
