using Arlen.CameraSystem;
using Arlen.Combat;
using Arlen.Player;
using UnityEngine;

namespace Arlen.Saving
{
    /// <summary>Saves the player's position, facing and health. Stamina is refilled on load (you just rested).</summary>
    [DisallowMultipleComponent]
    public class PlayerSaveState : MonoBehaviour, ISaveable
    {
        public void CaptureState(SaveData data)
        {
            data.player.hasPosition = true;
            data.player.position = transform.position;
            data.player.yaw = transform.eulerAngles.y;
            var health = GetComponent<HealthComponent>();
            data.player.healthFraction = health != null ? Mathf.Clamp01(health.Normalized) : 1f;
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            if (data.player == null) return;
            if (data.player.hasPosition)
            {
                var rotation = Quaternion.Euler(0f, data.player.yaw, 0f);
                var controller = GetComponent<ArlenPlayerController>();
                if (controller != null) controller.Teleport(data.player.position, rotation);
                else transform.SetPositionAndRotation(data.player.position, rotation);

                var cam = FindAnyObjectByType<ThirdPersonCameraController>();
                if (cam != null && cam.Target == transform) cam.SnapToTarget();
            }

            var health = GetComponent<HealthComponent>();
            if (health != null) health.Revive(Mathf.Clamp(data.player.healthFraction, 0.05f, 1f));
            var stamina = GetComponent<StaminaComponent>();
            if (stamina != null) stamina.Refill();
        }
    }
}
