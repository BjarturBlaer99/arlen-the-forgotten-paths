using UnityEngine;

namespace Arlen.Interaction
{
    /// <summary>
    /// Chest with an animated lid. Storage contents arrive with the inventory system (Milestone 4);
    /// for now opening shows a short description.
    /// </summary>
    public class StorageChest : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform lid;
        [SerializeField] float openAngle = -105f;
        [SerializeField, Min(1f)] float speed = 300f;
        [TextArea(2, 4)] [SerializeField] string[] openLines = { "Your spare gear. (Storage arrives with the inventory.)" };

        Quaternion closedRotation;
        float current;
        float target;

        public bool IsOpen => !Mathf.Approximately(target, 0f);
        public string InteractionPrompt => IsOpen ? "Close" : "Open";
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.5f;

        void Awake()
        {
            if (lid != null) closedRotation = lid.localRotation;
            enabled = false;
        }

        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor interactor)
        {
            bool opening = !IsOpen;
            target = opening ? openAngle : 0f;
            enabled = true;
            if (opening && interactor != null && interactor.Dialogue != null && openLines != null && openLines.Length > 0)
                interactor.Dialogue.Play(openLines);
        }

        void Update()
        {
            current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
            if (lid != null) lid.localRotation = closedRotation * Quaternion.Euler(current, 0f, 0f);
            if (Mathf.Approximately(current, target)) enabled = false;
        }
    }
}
