using UnityEngine;
using UnityEngine.Events;

namespace Arlen.Interaction
{
    /// <summary>
    /// Shows descriptive text when examined (maps, signs, keepsakes). Text appears as narration in the
    /// dialogue box. <see cref="onExamined"/> lets quests react later without new code here.
    /// </summary>
    public class Examinable : MonoBehaviour, IInteractable
    {
        [SerializeField] string verb = "Examine";
        [TextArea(2, 5)] [SerializeField] string[] lines = { "Nothing special." };
        [Tooltip("Optional point for range checks (defaults to this transform).")]
        [SerializeField] Transform point;
        [SerializeField] UnityEvent onExamined;

        public int TimesExamined { get; private set; }
        public string InteractionPrompt => verb;
        public Vector3 InteractionPoint => point != null ? point.position : transform.position;

        public bool CanInteract(PlayerInteractor interactor) => lines != null && lines.Length > 0;

        public void Interact(PlayerInteractor interactor)
        {
            TimesExamined++;
            if (interactor != null && interactor.Dialogue != null) interactor.Dialogue.Play(lines);
            onExamined?.Invoke();
        }

        public void SetText(string verbText, params string[] text)
        {
            verb = verbText;
            lines = text;
        }
    }
}
