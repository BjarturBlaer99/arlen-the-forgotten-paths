using Arlen.Core;
using Arlen.Interaction;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.Items
{
    /// <summary>
    /// An item lying in the world. Interact to put it in the pack; it then stays gone across saves
    /// (tracked by <see cref="pickupId"/>, which must be unique in the scene).
    /// </summary>
    public class ItemPickup : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] string pickupId;
        [SerializeField] ItemDefinition item;
        [SerializeField, Min(1)] int count = 1;
        [SerializeField] string verb = "Take";
        [Tooltip("Hidden once collected. Defaults to this GameObject.")]
        [SerializeField] GameObject hideOnCollect;
        [Tooltip("Optional narration after picking it up.")]
        [TextArea(2, 4)] [SerializeField] string[] collectLines;
        [TextArea(1, 2)] [SerializeField] string packFullLine = "Your pack is full.";

        public string PickupId => pickupId;
        public ItemDefinition Item => item;
        public int Count => count;
        public bool Collected { get; private set; }

        public string InteractionPrompt => item != null ? $"{verb} {item.displayName}" : verb;
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.3f;

        public void Configure(string id, ItemDefinition definition, int amount, GameObject hideTarget = null, params string[] lines)
        {
            pickupId = id;
            item = definition;
            count = Mathf.Max(1, amount);
            hideOnCollect = hideTarget;
            collectLines = lines;
        }

        public bool CanInteract(PlayerInteractor interactor) => !Collected && item != null;

        public void Interact(PlayerInteractor interactor)
        {
            if (Collected || item == null || interactor == null) return;
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            int added = inventory.Receive(item, count);
            if (added <= 0)
            {
                if (interactor.Dialogue != null && !string.IsNullOrEmpty(packFullLine)) interactor.Dialogue.Play(new[] { packFullLine });
                return;
            }

            WorldSignals.Raise(SignalKind.Collected, item.id, added);
            count -= added;
            if (count > 0) return; // partially taken; the rest stays here

            if (interactor.Dialogue != null && collectLines != null && collectLines.Length > 0) interactor.Dialogue.Play(collectLines);
            MarkCollected();
        }

        void MarkCollected()
        {
            Collected = true;
            (hideOnCollect != null ? hideOnCollect : gameObject).SetActive(false);
        }

        public void CaptureState(SaveData data)
        {
            if (Collected && !string.IsNullOrEmpty(pickupId)) data.collectedPickups.Add(pickupId);
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            if (!string.IsNullOrEmpty(pickupId) && data.collectedPickups.Contains(pickupId)) MarkCollected();
        }
    }
}
