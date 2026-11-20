using UnityEngine;
using UnityEngine.UI;

namespace Arlen.DialogueSystem
{
    /// <summary>
    /// uGUI presentation for dialogue: a bottom panel with a name plate, body text and a blinking
    /// continue marker. Swap this out for a styled version without touching the runner.
    /// </summary>
    public class DialogueView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] GameObject namePlate;
        [SerializeField] Text nameText;
        [SerializeField] Text bodyText;
        [SerializeField] GameObject continueMarker;

        string fullText = string.Empty;
        int shownChars = -1;
        bool complete;

        public bool IsVisible => root != null && root.activeSelf;

        public void Show()
        {
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void SetSpeaker(string speaker)
        {
            bool hasName = !string.IsNullOrEmpty(speaker);
            if (namePlate != null) namePlate.SetActive(hasName);
            if (nameText != null) nameText.text = speaker;
            if (bodyText != null) bodyText.fontStyle = hasName ? FontStyle.Normal : FontStyle.Italic;
        }

        public void SetText(string text, int visibleCharacters, bool isComplete)
        {
            complete = isComplete;
            if (bodyText == null) return;
            if (!ReferenceEquals(text, fullText) || visibleCharacters != shownChars)
            {
                fullText = text;
                shownChars = visibleCharacters;
                bodyText.text = visibleCharacters >= text.Length ? text : text.Substring(0, visibleCharacters);
            }
        }

        void Update()
        {
            if (continueMarker == null) return;
            bool show = complete && IsVisible && Mathf.Repeat(Time.unscaledTime, 1f) < 0.65f;
            if (continueMarker.activeSelf != show) continueMarker.SetActive(show);
        }
    }
}
