using System;
using Arlen.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>One inventory slot button: placeholder icon (colour + initials) and stack count.</summary>
    public class ItemSlotView : MonoBehaviour, ISelectHandler, IPointerEnterHandler, ICancelHandler
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Text initials;
        [SerializeField] Text count;
        [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.06f);

        public ItemDefinition Item { get; private set; }
        public int Count { get; private set; }
        public Button Button => button;

        public event Action<ItemSlotView> Clicked;
        public event Action<ItemSlotView> Focused;
        public event Action Cancelled;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Set(ItemDefinition item, int amount)
        {
            Item = item;
            Count = item != null ? amount : 0;
            if (icon != null) icon.color = item != null ? item.iconColor : emptyColor;
            if (initials != null) initials.text = item != null ? Abbreviate(item.displayName) : string.Empty;
            if (count != null) count.text = item != null && amount > 1 ? amount.ToString() : string.Empty;
        }

        public void OnSelect(BaseEventData eventData) => Focused?.Invoke(this);
        public void OnPointerEnter(PointerEventData eventData) => Focused?.Invoke(this);
        public void OnCancel(BaseEventData eventData) => Cancelled?.Invoke();

        /// <summary>"Pathwarden Sabre" → "PS", "Sunmoss" → "Su".</summary>
        public static string Abbreviate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var words = name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2) return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[words.Length - 1][0])}";
            string w = words[0];
            return w.Length >= 2 ? $"{char.ToUpperInvariant(w[0])}{char.ToLowerInvariant(w[1])}" : w.ToUpperInvariant();
        }
    }
}
