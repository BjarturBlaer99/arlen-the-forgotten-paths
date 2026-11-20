using Arlen.Combat;
using UnityEngine;

namespace Arlen.World
{
    /// <summary>
    /// Base for box-shaped areas that react when the player enters or leaves. Polls the player's position
    /// against an oriented box (cheap; no trigger colliders or rigidbodies required).
    /// </summary>
    public abstract class PlayerZone : MonoBehaviour
    {
        [Tooltip("Local-space size of the zone; the box sits on this transform's origin (bottom-centre).")]
        [SerializeField] protected Vector3 size = new Vector3(6f, 4f, 6f);
        [SerializeField, Min(0.02f)] float checkInterval = 0.1f;

        float timer;

        public bool PlayerInside { get; private set; }
        public Vector3 Size => size;

        public void SetSize(Vector3 value) => size = value;

        protected abstract void OnPlayerEntered();
        protected abstract void OnPlayerExited();

        protected virtual void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = checkInterval;

            bool inside = IsPlayerInside();
            if (inside == PlayerInside) return;
            PlayerInside = inside;
            if (inside) OnPlayerEntered();
            else OnPlayerExited();
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= size.x * 0.5f && local.y >= -0.5f && local.y <= size.y && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        bool IsPlayerInside()
        {
            var all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Team == Team.Player && Contains(all[i].transform.position)) return true;
            return false;
        }

        protected virtual void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
        }
    }
}
