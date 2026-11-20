using UnityEngine;

namespace Arlen.Interaction
{
    /// <summary>
    /// Hinged door that swings away from the player. Can be locked with a message (shown via dialogue).
    /// The leaf's collider moves with the hinge, so an open door doesn't block the doorway.
    /// Place this component on a root at the bottom-centre of the doorway, facing outward;
    /// the hinge is a child at one edge.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Tooltip("Rotates around its local Y axis. The door leaf (mesh + collider) is a child.")]
        [SerializeField] Transform hinge;
        [SerializeField] float openAngle = 105f;
        [SerializeField, Min(1f)] float swingSpeed = 260f;
        [SerializeField] bool locked;
        [TextArea(2, 4)] [SerializeField] string lockedMessage = "It's locked.";
        [SerializeField] string openVerb = "Open";
        [SerializeField] string closeVerb = "Close";
        [SerializeField] string lockedVerb = "Try door";

        Quaternion closedRotation;
        float targetAngle;
        float currentAngle;
        readonly string[] lockedLines = new string[1];

        public bool IsOpen => !Mathf.Approximately(targetAngle, 0f);
        public bool IsLocked { get => locked; set => locked = value; }
        public float CurrentAngle => currentAngle;

        public string InteractionPrompt => locked ? lockedVerb : IsOpen ? closeVerb : openVerb;
        public Vector3 InteractionPoint => transform.position + Vector3.up;

        void Awake()
        {
            if (hinge == null) hinge = transform;
            closedRotation = hinge.localRotation;
        }

        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor interactor)
        {
            if (locked)
            {
                lockedLines[0] = lockedMessage;
                if (interactor != null && interactor.Dialogue != null) interactor.Dialogue.Play(lockedLines);
                return;
            }

            if (IsOpen) targetAngle = 0f;
            else
            {
                // Swing away from whoever opens it.
                Vector3 toUser = interactor != null ? interactor.transform.position - transform.position : -transform.forward;
                // Hinge on the left (-X) edge: a positive yaw swings the leaf toward -Z (away from a user in front).
                float side = Vector3.Dot(toUser, transform.forward) >= 0f ? 1f : -1f;
                targetAngle = openAngle * side;
            }
            enabled = true;
        }

        public void SetOpen(bool open, bool instant = false)
        {
            targetAngle = open ? openAngle : 0f;
            if (instant) { currentAngle = targetAngle; Apply(); }
            enabled = true;
        }

        void Update()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
            Apply();
            if (Mathf.Approximately(currentAngle, targetAngle)) enabled = false; // sleep until used
        }

        void Apply() => hinge.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
    }
}
