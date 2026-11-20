using Arlen.Combat;
using UnityEngine;

namespace Arlen.UI
{
    /// <summary>
    /// Binds the player's health and stamina components to HUD bars via events. Gameplay components
    /// have no knowledge of the UI.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [SerializeField] HealthComponent health;
        [SerializeField] StaminaComponent stamina;
        [SerializeField] ResourceBar healthBar;
        [SerializeField] ResourceBar staminaBar;
        [Tooltip("Shown while the player is dead/respawning.")]
        [SerializeField] GameObject defeatedMessage;

        void OnEnable()
        {
            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
                health.Died += OnDied;
                health.Revived += OnRevived;
            }
            if (stamina != null)
            {
                stamina.StaminaChanged += OnStaminaChanged;
                stamina.Exhausted += OnExhausted;
                stamina.Recovered += OnRecovered;
                stamina.SpendFailed += OnSpendFailed;
            }
        }

        void Start()
        {
            if (health != null && healthBar != null) healthBar.SetValue(health.Normalized, instant: true);
            if (stamina != null && staminaBar != null) staminaBar.SetValue(stamina.Normalized, instant: true);
            if (defeatedMessage != null) defeatedMessage.SetActive(false);
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                health.Died -= OnDied;
                health.Revived -= OnRevived;
            }
            if (stamina != null)
            {
                stamina.StaminaChanged -= OnStaminaChanged;
                stamina.Exhausted -= OnExhausted;
                stamina.Recovered -= OnRecovered;
                stamina.SpendFailed -= OnSpendFailed;
            }
        }

        void OnHealthChanged(float current, float max) { if (healthBar != null) healthBar.SetValue(current / max); }
        void OnStaminaChanged(float current, float max) { if (staminaBar != null) staminaBar.SetValue(current / max); }
        void OnExhausted() { if (staminaBar != null) staminaBar.SetAlert(true); }
        void OnRecovered() { if (staminaBar != null) staminaBar.SetAlert(false); }
        void OnSpendFailed() { if (staminaBar != null) staminaBar.Flash(); }
        void OnDied() { if (defeatedMessage != null) defeatedMessage.SetActive(true); }
        void OnRevived() { if (defeatedMessage != null) defeatedMessage.SetActive(false); }
    }
}
