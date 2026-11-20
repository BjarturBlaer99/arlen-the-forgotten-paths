using System.Collections;
using Arlen.Combat;
using UnityEngine;

namespace Arlen.Player
{
    /// <summary>
    /// Prototype death handling: disables control on death, then respawns at the spawn point with
    /// full health and stamina. Will be replaced by checkpoint/save-based respawning in Milestone 4.
    /// Also respawns the player if they fall out of the world.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] HealthComponent health;
        [SerializeField] StaminaComponent stamina;
        [SerializeField] ArlenPlayerController controller;
        [SerializeField] PlayerActionState actionState;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Optional. Defaults to the position the player starts at.")]
        [SerializeField] Transform spawnPoint;
        [SerializeField, Min(0f)] float respawnDelay = 2.5f;
        [SerializeField] float killHeight = -30f;
        [Tooltip("Components disabled while dead (e.g. dodge, combat).")]
        [SerializeField] Behaviour[] disableWhileDead;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        Coroutine routine;

        void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();
            if (stamina == null) stamina = GetComponent<StaminaComponent>();
            if (controller == null) controller = GetComponent<ArlenPlayerController>();
            if (actionState == null) actionState = GetComponent<PlayerActionState>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        void Update()
        {
            if (transform.position.y < killHeight && routine == null) Respawn();
        }

        void OnDied()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            SetControlEnabled(false);
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
        }

        public void Respawn()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (actionState != null) actionState.ForceClear();
            if (controller != null) controller.Teleport(spawnPosition, spawnRotation);
            if (health != null) health.Revive();
            if (stamina != null) stamina.Refill();
            if (input != null) input.ClearBufferedInput();
            SetControlEnabled(true);
        }

        void SetControlEnabled(bool value)
        {
            if (controller != null) controller.enabled = value;
            if (disableWhileDead == null) return;
            foreach (var b in disableWhileDead)
                if (b != null) b.enabled = value;
        }
    }
}
