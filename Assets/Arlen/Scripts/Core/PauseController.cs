using System;
using Arlen.CameraSystem;
using Arlen.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arlen.Core
{
    /// <summary>
    /// Pauses via timeScale, blocks gameplay input and shows the pause menu. Also re-locks the cursor
    /// when the player clicks back into the game window.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseController : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [Tooltip("Root of the pause menu UI. Toggled active/inactive.")]
        [SerializeField] GameObject pauseMenuRoot;

        public bool IsPaused { get; private set; }
        public event Action<bool> PauseChanged;

        void Awake()
        {
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (input != null) input.PausePressed += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.PausePressed -= Toggle;
            if (IsPaused) SetPaused(false);
        }

        void Update()
        {
            // Clicking the Game view after Escape/alt-tab recaptures the mouse.
            if (!IsPaused && Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ThirdPersonCameraController.SetCursorLocked(true);
                if (input != null) input.ClearBufferedInput(); // don't swing the sword on that click
            }
        }

        public void Toggle() => SetPaused(!IsPaused);
        public void Resume() => SetPaused(false);

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (input != null)
            {
                if (paused) input.PushBlock();
                else input.PopBlock();
            }
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(paused);
            ThirdPersonCameraController.SetCursorLocked(!paused);
            PauseChanged?.Invoke(paused);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
