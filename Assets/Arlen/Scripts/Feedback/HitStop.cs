using UnityEngine;

namespace Arlen.Feedback
{
    /// <summary>
    /// Brief slow-down on impactful hits. Only touches timeScale while the game runs at normal speed,
    /// and only restores it if nothing else (e.g. pause) changed it in the meantime.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float slowScale = 0.05f;
        [Tooltip("Global toggle, e.g. for an accessibility option later.")]
        [SerializeField] bool effectEnabled = true;

        float remaining;
        bool active;

        public void Trigger(float duration)
        {
            if (!effectEnabled || duration <= 0f) return;
            if (!active)
            {
                if (!Mathf.Approximately(Time.timeScale, 1f)) return; // paused or slowed elsewhere
                Time.timeScale = slowScale;
                active = true;
            }
            remaining = Mathf.Max(remaining, duration);
        }

        void Update()
        {
            if (!active) return;
            remaining -= Time.unscaledDeltaTime;
            if (remaining > 0f) return;
            active = false;
            if (Mathf.Approximately(Time.timeScale, slowScale)) Time.timeScale = 1f;
        }

        void OnDisable()
        {
            if (active && Mathf.Approximately(Time.timeScale, slowScale)) Time.timeScale = 1f;
            active = false;
        }
    }
}
