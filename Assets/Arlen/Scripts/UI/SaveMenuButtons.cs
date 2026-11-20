using Arlen.Saving;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>Pause-menu entries for loading the last rest and starting over (with a confirmation step).</summary>
    public class SaveMenuButtons : MonoBehaviour
    {
        [SerializeField] SaveManager saves;
        [SerializeField] Button loadButton;
        [SerializeField] GameObject confirmNewGamePanel;
        [SerializeField] Button cancelNewGameButton;

        void OnEnable()
        {
            if (loadButton != null) loadButton.interactable = saves != null && saves.HasSave;
            if (confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
        }

        public void LoadLastSave()
        {
            if (saves != null) saves.LoadLastSave();
        }

        public void AskNewGame()
        {
            if (confirmNewGamePanel == null) { ConfirmNewGame(); return; }
            confirmNewGamePanel.SetActive(true);
            if (cancelNewGameButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(cancelNewGameButton.gameObject); // safe default for gamepads
        }

        public void ConfirmNewGame()
        {
            if (saves != null) saves.StartNewGame();
        }

        public void CancelNewGame()
        {
            if (confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
        }
    }
}
