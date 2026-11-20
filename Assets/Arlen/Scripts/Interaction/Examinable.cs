using Arlen.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Arlen.Interaction
{
    /// <summary>
    /// Shows descriptive text when examined (maps, signs, keepsakes). Text appears as narration in the
    /// dialogue box. Raises a <see cref="SignalKind.Examined"/> world signal when it has an id, so quests can
    /// react; <see cref="onExamined"/> is there for one-off scene wiring.
    /// </summary>
    public class Examinable : MonoBehaviour, IInteractable
    {
        [Tooltip("Optional stable id for quests (e.g. \"vanished_road\").")]
        [SerializeField] string examineId;
        [SerializeField] string verb = "Examine";
        [TextArea(2, 5)] [SerializeField] string[] lines = { "Nothing special." };
        [Tooltip("Optional point for range checks (defaults to this transform).")]
        [SerializeField] Transform point;
        [SerializeField] UnityEvent onExamined;

        public int TimesExamined { get; private set; }
        public string ExamineId => examineId;
        public string InteractionPrompt => verb;
        public Vector3 InteractionPoint => point != null ? point.position : transform.position;

        public bool CanInteract(PlayerInteractor interactor) => lines != null && lines.Length > 0;

        public void Interact(PlayerInteractor interactor)
        {
            TimesExamined++;
            if (interactor != null && interactor.Dialogue != null) interactor.Dialogue.Play(lines);
            onExamined?.Invoke();
            WorldSignals.Raise(SignalKind.Examined, examineId);
        }

        public void SetId(string id) => examineId = id;

        public void SetText(string verbText, params string[] text)
        {
            verb = verbText;
            lines = text;
        }
    }
}
