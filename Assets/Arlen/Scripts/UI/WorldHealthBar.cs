using Arlen.Combat;
using UnityEngine;

namespace Arlen.UI
{
    /// <summary>
    /// Minimal overhead health bar built from two quads/cubes (no Canvas per enemy). Appears when the
    /// owner is damaged, faces the camera and hides again after a few seconds or on death.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] HealthComponent health;
        [Tooltip("Scaled along local X from the left edge to show remaining health.")]
        [SerializeField] Transform fill;
        [Tooltip("Root of the visible bar (toggled).")]
        [SerializeField] GameObject visualRoot;
        [SerializeField, Min(0f)] float visibleAfterHit = 5f;

        Camera cam;
        float fullWidth = 1f;
        float hideTimer;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<HealthComponent>();
            if (fill != null) fullWidth = fill.localScale.x;
            if (visualRoot != null) visualRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (health == null) return;
            health.HealthChanged += OnChanged;
            health.Died += Hide;
        }

        void OnDisable()
        {
            if (health == null) return;
            health.HealthChanged -= OnChanged;
            health.Died -= Hide;
        }

        void OnChanged(float current, float max)
        {
            if (fill != null)
            {
                float ratio = max > 0f ? current / max : 0f;
                Vector3 s = fill.localScale;
                s.x = fullWidth * ratio;
                fill.localScale = s;
                Vector3 p = fill.localPosition;
                p.x = -(fullWidth - s.x) * 0.5f; // keep left-aligned
                fill.localPosition = p;
            }
            if (current < max && current > 0f)
            {
                hideTimer = visibleAfterHit;
                if (visualRoot != null) visualRoot.SetActive(true);
            }
            else if (current >= max) Hide();
        }

        void Hide()
        {
            hideTimer = 0f;
            if (visualRoot != null) visualRoot.SetActive(false);
        }

        void LateUpdate()
        {
            if (visualRoot == null || !visualRoot.activeSelf) return;
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f) { Hide(); return; }

            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
