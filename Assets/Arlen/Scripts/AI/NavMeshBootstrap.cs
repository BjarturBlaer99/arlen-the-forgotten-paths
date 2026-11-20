using Unity.AI.Navigation;
using UnityEngine;

namespace Arlen.AI
{
    /// <summary>
    /// Builds the NavMeshSurface at runtime if no baked data is assigned (e.g. after editing the greybox
    /// without re-baking). Runs before enemies enable so their agents can be placed.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface.navMeshData != null) return;
            Debug.Log("[Arlen] No baked NavMesh found; building at runtime. Bake the NavMeshSurface to skip this.", this);
            surface.BuildNavMesh();
        }
    }
}
