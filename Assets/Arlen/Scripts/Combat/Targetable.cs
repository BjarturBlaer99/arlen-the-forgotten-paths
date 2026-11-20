using System.Collections.Generic;
using UnityEngine;

namespace Arlen.Combat
{
    public enum Team
    {
        Player,
        Enemy,
        Neutral,
    }

    /// <summary>
    /// Marks something AI can perceive and attack. Keeps a static registry of active targets so sensors
    /// don't need FindObjectsOfType or tags. Also used to prevent friendly fire between enemies.
    /// </summary>
    [DisallowMultipleComponent]
    public class Targetable : MonoBehaviour
    {
        static readonly List<Targetable> Active = new List<Targetable>(16);
        public static IReadOnlyList<Targetable> All => Active;

        [SerializeField] Team team = Team.Neutral;
        [Tooltip("Point AI looks/aims at (e.g. chest). Defaults to this transform + 1 m.")]
        [SerializeField] Transform aimPoint;
        [SerializeField] HealthComponent health;

        public Team Team => team;
        public HealthComponent Health => health;
        public bool IsAlive => health == null || health.IsAlive;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up;

        public static bool AreHostile(Team a, Team b) => a != b && a != Team.Neutral && b != Team.Neutral;

        public void Configure(Team value) => team = value;

        void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }
}
