using System;
using System.Collections.Generic;
using System.IO;
using Arlen.Interaction;
using Arlen.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arlen.Saving
{
    /// <summary>
    /// Single-slot save system. Saves when the player rests (<see cref="RestSpot.AnyRested"/>) and restores
    /// the save on scene start when it belongs to this scene. Loading mid-game reloads the saved scene and
    /// restores from scratch, so world objects never have to "undo" their current state.
    /// </summary>
    [DefaultExecutionOrder(-50)] // Start before QuestManager/HUD so they see restored state
    [DisallowMultipleComponent]
    public class SaveManager : MonoBehaviour
    {
        [SerializeField] ItemDatabase items;
        [SerializeField] string fileName = "arlen_save_0.json";
        [Tooltip("Restore the save (if it belongs to this scene) when the scene starts.")]
        [SerializeField] bool loadOnStart = true;
        [SerializeField] bool saveWhenResting = true;

        /// <summary>Tests point this at a temp file so they never touch the player's real save.</summary>
        public static string PathOverride { get; set; }

        public string SavePath => !string.IsNullOrEmpty(PathOverride) ? PathOverride : Path.Combine(Application.persistentDataPath, fileName);
        public bool HasSave => SaveFile.Exists(SavePath);
        public ItemDatabase Items => items;

        /// <summary>Raised after a successful save.</summary>
        public event Action Saved;
        /// <summary>Raised after a save was restored into the scene.</summary>
        public event Action Restored;

        public void Configure(ItemDatabase database, bool restoreOnStart = true)
        {
            items = database;
            loadOnStart = restoreOnStart;
        }

        void OnEnable() => RestSpot.AnyRested += OnRested;
        void OnDisable() => RestSpot.AnyRested -= OnRested;

        void Start()
        {
            if (!loadOnStart) return;
            var result = SaveFile.Read(SavePath, out var data);
            if (result == SaveReadResult.Ok)
            {
                if (data.sceneName == SceneManager.GetActiveScene().name) RestoreInto(data);
            }
            else if (result != SaveReadResult.Missing)
            {
                Debug.LogWarning($"[Arlen] Save at '{SavePath}' could not be loaded ({result}); starting fresh.", this);
            }
        }

        void OnRested(RestSpot spot)
        {
            if (saveWhenResting) Save();
        }

        static List<ISaveable> FindSaveables()
        {
            var list = new List<ISaveable>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb is ISaveable s) list.Add(s);
            return list;
        }

        /// <summary>Gathers the current scene state.</summary>
        public SaveData Capture()
        {
            var data = new SaveData
            {
                sceneName = SceneManager.GetActiveScene().name,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            foreach (var s in FindSaveables()) s.CaptureState(data);
            return data;
        }

        public bool Save()
        {
            try
            {
                SaveFile.Write(SavePath, Capture());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[Arlen] Saving failed: {e.Message}", this);
                return false;
            }
            Saved?.Invoke();
            return true;
        }

        /// <summary>Applies saved state to the objects in the current scene.</summary>
        public void RestoreInto(SaveData data)
        {
            if (data == null) return;
            var context = new SaveContext(items);
            foreach (var s in FindSaveables()) s.RestoreState(data, context);
            Restored?.Invoke();
        }

        /// <summary>Reloads the scene the save was made in; it restores itself on start.</summary>
        public void LoadLastSave()
        {
            if (SaveFile.Read(SavePath, out var data) != SaveReadResult.Ok) return;
            Time.timeScale = 1f;
            string scene = string.IsNullOrEmpty(data.sceneName) ? SceneManager.GetActiveScene().name : data.sceneName;
            SceneManager.LoadScene(scene);
        }

        /// <summary>Erases the save and restarts the current scene from its authored state.</summary>
        public void StartNewGame()
        {
            SaveFile.Delete(SavePath);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void DeleteSave() => SaveFile.Delete(SavePath);
    }
}
