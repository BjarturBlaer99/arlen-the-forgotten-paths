using Arlen.Combat;
using UnityEngine;

namespace Arlen.AI
{
    /// <summary>
    /// Procedural presentation for the primitive Boar Ruffian placeholder: walk bob, a readable club
    /// raise during the telegraph (eyes glow), swing, stagger wobble, "!" alert marker and death topple.
    /// Reads brain/attack state only; replace with an Animator-driven presenter for production art.
    /// </summary>
    public class BoarRuffianVisual : MonoBehaviour
    {
        [SerializeField] EnemyController controller;
        [SerializeField] EnemyMeleeAttack attack;
        [SerializeField] EnemyMotor motor;
        [SerializeField] Transform body;
        [SerializeField] Transform clubPivot;
        [SerializeField] Renderer[] eyes;
        [SerializeField] GameObject alertMarker;

        [SerializeField] Color eyeIdle = new Color(0.15f, 0.1f, 0.05f);
        [SerializeField] Color eyeTelegraph = new Color(1f, 0.25f, 0.1f);
        [SerializeField] Color eyeStagger = new Color(0.9f, 0.9f, 1f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Vector3 bodyRestPos;
        Quaternion bodyRestRot, clubRestRot;
        Quaternion clubRaised, clubStruck;
        MaterialPropertyBlock block;
        float walkPhase;
        float deathTime = -1f;
        Vector3 deathAxis = Vector3.forward;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<EnemyController>();
            if (attack == null) attack = GetComponentInParent<EnemyMeleeAttack>();
            if (motor == null) motor = GetComponentInParent<EnemyMotor>();
            block = new MaterialPropertyBlock();
            if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
            if (clubPivot != null)
            {
                clubRestRot = clubPivot.localRotation;
                clubRaised = clubRestRot * Quaternion.Euler(-130f, 0f, 20f);
                clubStruck = clubRestRot * Quaternion.Euler(60f, 0f, -10f);
            }
        }

        void OnEnable()
        {
            if (controller != null) controller.StateChanged += OnStateChanged;
            deathTime = -1f;
            if (body != null) body.localRotation = bodyRestRot;
        }

        void OnDisable()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(EnemyState previous, EnemyState next)
        {
            if (next == EnemyState.Dead)
            {
                deathTime = Time.time;
                deathAxis = Random.value > 0.5f ? Vector3.forward : Vector3.back;
            }
            else if (previous == EnemyState.Dead)
            {
                deathTime = -1f;
            }
        }

        void LateUpdate()
        {
            if (controller == null) return;
            var state = controller.State;

            if (alertMarker != null) alertMarker.SetActive(state == EnemyState.Alert);

            Color eyeColor = eyeIdle;
            Vector3 pos = bodyRestPos;
            Quaternion rot = bodyRestRot;
            Quaternion club = clubRestRot;

            if (state == EnemyState.Dead && deathTime >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - deathTime) / 0.6f);
                rot = bodyRestRot * Quaternion.AngleAxis(85f * t * t, deathAxis);
                pos = bodyRestPos + Vector3.down * 0.35f * t;
            }
            else if (state == EnemyState.Stagger)
            {
                float t = controller.StateTime;
                float amp = controller.LastStaggerFromParry ? 14f : 9f;
                rot = bodyRestRot * Quaternion.Euler(-amp + Mathf.Sin(t * 18f) * amp * 0.5f, 0f, Mathf.Sin(t * 11f) * amp * 0.6f);
                club = clubRestRot * Quaternion.Euler(-40f, 0f, 40f);
                eyeColor = Color.Lerp(eyeIdle, eyeStagger, 0.5f + 0.5f * Mathf.Sin(t * 20f));
            }
            else
            {
                float speed = motor != null ? motor.Speed : 0f;
                walkPhase += Time.deltaTime * speed * 4f;
                pos += Vector3.up * Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f * Mathf.Clamp01(speed);
                rot = bodyRestRot * Quaternion.Euler(Mathf.Clamp(speed * 2.5f, 0f, 10f), 0f, Mathf.Sin(walkPhase) * 3f);
            }

            if (attack != null && attack.IsAttacking)
            {
                float p = attack.PhaseProgress;
                switch (attack.Phase)
                {
                    case EnemyAttackPhase.Windup:
                        club = Quaternion.Slerp(clubRestRot, clubRaised, 1f - (1f - p) * (1f - p));
                        rot = bodyRestRot * Quaternion.Euler(-12f * p, 0f, 0f); // rear back
                        eyeColor = Color.Lerp(eyeIdle, eyeTelegraph, p);
                        break;
                    case EnemyAttackPhase.Active:
                        club = Quaternion.Slerp(clubRaised, clubStruck, p);
                        rot = bodyRestRot * Quaternion.Euler(15f, 0f, 0f);
                        eyeColor = eyeTelegraph;
                        break;
                    case EnemyAttackPhase.Recovery:
                        club = Quaternion.Slerp(clubStruck, clubRestRot, p * p);
                        rot = bodyRestRot * Quaternion.Euler(15f * (1f - p), 0f, 0f);
                        break;
                }
            }

            if (body != null)
            {
                body.localPosition = pos;
                body.localRotation = rot;
            }
            if (clubPivot != null) clubPivot.localRotation = club;
            SetEyes(eyeColor);
        }

        void SetEyes(Color c)
        {
            if (eyes == null) return;
            foreach (var r in eyes)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(EmissionId, c * 2f);
                r.SetPropertyBlock(block);
            }
        }
    }
}
