using Arlen.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>Shows "[E] Open" style prompts for the interactor's current focus. Event-driven.</summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] GameObject root;
        [SerializeField] Text label;
        [Tooltip("{0} is replaced by the interactable's verb.")]
        [SerializeField] string format = "<b>[E]</b>  {0}";

        IInteractable current;
        string lastPrompt;

        public bool IsVisible => root != null && root.activeSelf;
        public string CurrentText => label != null ? label.text : string.Empty;

        void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        void OnEnable()
        {
            if (interactor != null) interactor.FocusChanged += OnFocusChanged;
        }

        void OnDisable()
        {
            if (interactor != null) interactor.FocusChanged -= OnFocusChanged;
        }

        void OnFocusChanged(IInteractable focus)
        {
            current = focus;
            lastPrompt = null;
            Refresh();
        }

        void Update()
        {
            // Prompts can change while focused (e.g. a door that is opening); cheap string compare.
            if (current != null && current.InteractionPrompt != lastPrompt) Refresh();
        }

        void Refresh()
        {
            bool show = current != null;
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (!show || label == null) return;
            lastPrompt = current.InteractionPrompt;
            label.text = string.Format(format, lastPrompt);
        }
    }
}
