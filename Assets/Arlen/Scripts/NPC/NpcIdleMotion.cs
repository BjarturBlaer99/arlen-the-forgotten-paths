using UnityEngine;

namespace Arlen.NPC
{
    /// <summary>
    /// Procedural idle for primitive placeholder villagers: breathing bob, tail sway and a little
    /// "talking" bounce. Presentation only; remove when real animations arrive.
    /// </summary>
    public class NpcIdleMotion : MonoBehaviour
    {
        [SerializeField] NpcCharacter npc;
        [SerializeField] Transform body;
        [SerializeField] Transform tail;
        [SerializeField] float breatheAmplitude = 0.015f;
        [SerializeField] float breatheSpeed = 2.2f;

        Vector3 bodyRest;
        Quaternion tailRest;
        float phase;

        void Awake()
        {
            if (npc == null) npc = GetComponentInParent<NpcCharacter>();
            if (body != null) bodyRest = body.localPosition;
            if (tail != null) tailRest = tail.localRotation;
            phase = Random.value * 10f; // villagers don't breathe in sync
        }

        void LateUpdate()
        {
            float t = Time.time + phase;
            bool talking = npc != null && npc.IsTalking;
            if (body != null)
            {
                float bob = Mathf.Sin(t * breatheSpeed) * breatheAmplitude;
                if (talking) bob += Mathf.Abs(Mathf.Sin(t * 9f)) * 0.025f;
                body.localPosition = bodyRest + Vector3.up * bob;
            }
            if (tail != null)
                tail.localRotation = tailRest * Quaternion.Euler(0f, Mathf.Sin(t * (talking ? 3.5f : 1.3f)) * 18f, 0f);
        }
    }
}
