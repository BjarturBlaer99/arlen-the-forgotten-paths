using Arlen.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>HUD objective text: the tracked quest's title and current objective. Hidden when no quest is active.</summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField] QuestManager quests;
        [SerializeField] GameObject root;
        [SerializeField] Text title;
        [SerializeField] Text objective;

        public string ObjectiveText => objective != null ? objective.text : string.Empty;
        public bool IsShowing => root != null && root.activeSelf;

        public void Configure(QuestManager manager) => quests = manager;

        void OnEnable()
        {
            if (quests != null) quests.Log.Changed += OnChanged;
            Refresh();
        }

        void OnDisable()
        {
            if (quests != null) quests.Log.Changed -= OnChanged;
        }

        void OnChanged(QuestState _) => Refresh();

        public void Refresh()
        {
            var tracked = quests != null ? quests.Log.Tracked : null;
            var current = tracked?.CurrentObjective;
            bool show = current != null;
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (!show) return;

            if (title != null) title.text = tracked.Definition.title;
            if (objective != null)
            {
                string progress = current.requiredCount > 1 ? $" ({tracked.Progress}/{current.requiredCount})" : string.Empty;
                objective.text = $"• {current.description}{progress}";
            }
        }
    }
}
