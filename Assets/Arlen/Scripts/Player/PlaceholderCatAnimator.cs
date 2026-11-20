using Arlen.Combat;
using UnityEngine;

namespace Arlen.Player
{
    /// <summary>
    /// Procedural motion for the primitive-based placeholder cat (no Animator needed): run bob,
    /// lean, tail sway, sword swing arcs per combo step and a forward roll for dodges.
    /// Purely presentational; delete it when the rigged Blender model arrives.
    /// </summary>
    public class PlaceholderCatAnimator : MonoBehaviour
    {
        [SerializeField] ArlenPlayerController controller;
        [SerializeField] PlayerDodge dodge;
        [SerializeField] PlayerMeleeCombat combat;
        [Tooltip("Child that holds the body meshes; bobbed, leaned and rolled.")]
        [SerializeField] Transform body;
        [SerializeField] Transform tail;
        [SerializeField] Transform swordPivot;

        [SerializeField] float bobHeight = 0.06f;
        [SerializeField] float leanAngle = 8f;

        static readonly Vector3[] SwingFrom =
        {
            new Vector3(-20f, -100f, 0f), // step 1: right-to-left
            new Vector3(-20f, 100f, 0f),  // step 2: left-to-right
            new Vector3(-80f, 0f, 0f),    // step 3: overhead
        };
        static readonly Vector3[] SwingTo =
        {
            new Vector3(20f, 80f, 0f),
            new Vector3(20f, -80f, 0f),
            new Vector3(70f, 0f, 0f),
        };

        Vector3 bodyRestPos;
        Quaternion bodyRestRot, tailRestRot, swordRestRot;
        float stepPhase;
        float lean;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<ArlenPlayerController>();
            if (dodge == null) dodge = GetComponentInParent<PlayerDodge>();
            if (combat == null) combat = GetComponentInParent<PlayerMeleeCombat>();
            if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
            if (tail != null) tailRestRot = tail.localRotation;
            if (swordPivot != null) swordRestRot = swordPivot.localRotation;
        }

        void LateUpdate()
        {
            if (controller == null) return;
            float dt = Time.deltaTime;
            float speed = controller.PlanarSpeed;

            // Body bob & lean
            if (body != null)
            {
                stepPhase += dt * Mathf.Lerp(0f, 14f, speed / Mathf.Max(0.1f, controller.SprintSpeed));
                float bob = controller.IsGrounded ? Mathf.Abs(Mathf.Sin(stepPhase)) * bobHeight * Mathf.Clamp01(speed / 3f) : 0f;
                lean = Mathf.Lerp(lean, Mathf.Clamp01(speed / controller.SprintSpeed) * leanAngle, 1f - Mathf.Exp(-10f * dt));

                Quaternion rot = bodyRestRot * Quaternion.Euler(lean, 0f, 0f);
                if (dodge != null && dodge.IsDodging)
                {
                    // Full forward roll (or a small hop back for backsteps), around the body centre.
                    float angle = dodge.IsBackstep ? -25f * Mathf.Sin(dodge.NormalizedTime * Mathf.PI) : 360f * dodge.NormalizedTime;
                    rot = bodyRestRot * Quaternion.Euler(angle, 0f, 0f);
                    bob = dodge.IsBackstep ? 0f : -0.35f * Mathf.Sin(dodge.NormalizedTime * Mathf.PI);
                }
                body.localPosition = bodyRestPos + Vector3.up * bob;
                body.localRotation = rot;
            }

            // Tail sway
            if (tail != null)
            {
                float sway = Mathf.Sin(Time.time * (2f + speed * 0.6f)) * (10f + speed * 2f);
                tail.localRotation = tailRestRot * Quaternion.Euler(-speed * 4f, sway, 0f);
            }

            // Sword swing
            if (swordPivot != null)
            {
                if (combat != null && combat.IsAttacking)
                {
                    int i = Mathf.Clamp(combat.StepIndex, 0, SwingFrom.Length - 1);
                    Quaternion from = swordRestRot * Quaternion.Euler(SwingFrom[i]);
                    Quaternion to = swordRestRot * Quaternion.Euler(SwingTo[i]);
                    float p = combat.PhaseProgress;
                    swordPivot.localRotation = combat.Phase switch
                    {
                        AttackPhase.Windup => Quaternion.Slerp(swordRestRot, from, EaseOut(p)),
                        AttackPhase.Active => Quaternion.Slerp(from, to, EaseOut(p)),
                        _ => Quaternion.Slerp(to, swordRestRot, p * p),
                    };
                }
                else
                {
                    swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, swordRestRot, 1f - Mathf.Exp(-12f * dt));
                }
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
