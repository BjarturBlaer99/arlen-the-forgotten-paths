using System.Collections;
using UnityEngine;

namespace Arlen.AI
{
    /// <summary>
    /// Prototype helper: brings an enemy back a few seconds after its corpse disappears so combat can
    /// be practised repeatedly. Lives on a separate object because the enemy deactivates itself.
    /// Will be replaced by encounter/spawn logic tied to world state and saving.
    /// </summary>
    public class PrototypeEnemySpawner : MonoBehaviour
    {
        [SerializeField] EnemyController enemy;
        [SerializeField, Min(0f)] float respawnDelay = 3f;

        void OnEnable()
        {
            if (enemy != null) enemy.CorpseExpired += OnCorpseExpired;
        }

        void OnDisable()
        {
            if (enemy != null) enemy.CorpseExpired -= OnCorpseExpired;
        }

        void OnCorpseExpired() => StartCoroutine(Respawn());

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            if (enemy != null) enemy.ResetToHome();
        }
    }
}
