using UnityEngine;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>
    /// Horizontal bar driven by anchor stretching (no sprites needed). Has an optional trailing
    /// "recent loss" bar that catches up after a short delay, and a flash for failed actions.
    /// </summary>
    public class ResourceBar : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] RectTransform trail;
        [SerializeField] Graphic fillGraphic;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color alertColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField, Min(0f)] float trailDelay = 0.4f;
        [SerializeField, Min(0f)] float trailSpeed = 1.2f;
        [SerializeField, Min(0f)] float fillSpeed = 6f;

        float target = 1f;
        float shown = 1f;
        float trailValue = 1f;
        float trailTimer;
        float flashTimer;
        bool alert;

        public float Value => target;

        public void SetValue(float normalized, bool instant = false)
        {
            normalized = Mathf.Clamp01(normalized);
            if (normalized < target) trailTimer = trailDelay;
            target = normalized;
            if (instant)
            {
                shown = trailValue = target;
                Apply();
            }
        }

        /// <summary>Persistent alert tint (e.g. stamina exhausted).</summary>
        public void SetAlert(bool value)
        {
            alert = value;
            Apply();
        }

        /// <summary>Short flash (e.g. not enough stamina for an action).</summary>
        public void Flash(float seconds = 0.3f) => flashTimer = seconds;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            shown = Mathf.MoveTowards(shown, target, fillSpeed * dt);
            if (trailTimer > 0f) trailTimer -= dt;
            else trailValue = Mathf.MoveTowards(trailValue, shown, trailSpeed * dt);
            if (trailValue < shown) trailValue = shown;
            if (flashTimer > 0f) flashTimer -= dt;
            Apply();
        }

        void Apply()
        {
            SetWidth(fill, shown);
            SetWidth(trail, trailValue);
            if (fillGraphic != null)
            {
                bool flashing = flashTimer > 0f && Mathf.Repeat(flashTimer, 0.1f) > 0.05f;
                fillGraphic.color = alert || flashing ? alertColor : normalColor;
            }
        }

        static void SetWidth(RectTransform rt, float value)
        {
            if (rt == null) return;
            Vector2 max = rt.anchorMax;
            max.x = value;
            rt.anchorMax = max;
        }
    }
}
