using System;
using Arlen.Items;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.Interaction
{
    /// <summary>
    /// Chest with an animated lid and its own <see cref="Inventory"/>. Opening shows the chest/pack transfer
    /// screen; the lid closes when the screen does. Contents are saved under <see cref="containerId"/>.
    /// Without a screen (or without storage configured) it falls back to describing its contents.
    /// </summary>
    public class StorageChest : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] Transform lid;
        [SerializeField] float openAngle = -105f;
        [SerializeField, Min(1f)] float speed = 300f;
        [TextArea(2, 4)] [SerializeField] string[] openLines = { "Your spare gear." };

        [Header("Storage")]
        [Tooltip("Stable, unique id for saving. Leave empty for a decorative chest.")]
        [SerializeField] string containerId;
        [SerializeField] string displayName = "Chest";
        [SerializeField, Min(1)] int capacity = 24;
        [SerializeField] ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        Quaternion closedRotation;
        bool rotationCaptured;
        float current;
        float target;
        Inventory contents;

        public bool IsOpen => !Mathf.Approximately(target, 0f);
        public bool HasStorage => !string.IsNullOrEmpty(containerId);
        public string ContainerId => containerId;
        public string DisplayName => displayName;
        public string InteractionPrompt => IsOpen ? "Close" : "Open";
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.5f;

        /// <summary>Lazily created so it is valid regardless of Awake order (tests, save restore).</summary>
        public Inventory Contents
        {
            get
            {
                if (contents == null)
                {
                    contents = new Inventory(capacity);
                    foreach (var a in startingItems)
                        if (a.item != null) contents.Add(a.item, Mathf.Max(1, a.count));
                }
                return contents;
            }
        }

        public void ConfigureStorage(string id, string chestName, params ItemAmount[] items)
        {
            containerId = id;
            displayName = chestName;
            startingItems = items ?? Array.Empty<ItemAmount>();
            contents = null;
        }

        void Awake()
        {
            CaptureRotation();
            enabled = false;
        }

        void CaptureRotation()
        {
            if (rotationCaptured || lid == null) return;
            rotationCaptured = true;
            closedRotation = lid.localRotation;
        }

        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor interactor)
        {
            var screen = interactor != null ? interactor.InventoryScreen : null;
            if (HasStorage && screen != null)
            {
                SetLid(true);
                if (!screen.OpenContainer(this, () => SetLid(false))) SetLid(false);
                return;
            }

            bool opening = !IsOpen;
            SetLid(opening);
            if (opening && interactor != null && interactor.Dialogue != null && openLines != null && openLines.Length > 0)
                interactor.Dialogue.Play(openLines);
        }

        void SetLid(bool open)
        {
            CaptureRotation();
            target = open ? openAngle : 0f;
            enabled = true;
        }

        void Update()
        {
            // Unscaled: the inventory screen pauses the game while the lid is opening.
            current = Mathf.MoveTowards(current, target, speed * Time.unscaledDeltaTime);
            if (lid != null) lid.localRotation = closedRotation * Quaternion.Euler(current, 0f, 0f);
            if (Mathf.Approximately(current, target)) enabled = false;
        }

        public void CaptureState(SaveData data)
        {
            if (!HasStorage) return;
            data.containers.Add(new ContainerSaveData { id = containerId, items = Contents.Capture() });
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            if (!HasStorage) return;
            var saved = data.FindContainer(containerId);
            if (saved != null) Contents.Restore(saved.items, context.ResolveItem);
        }
    }
}
