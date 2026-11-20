using Arlen.Player;
using UnityEngine;

namespace Arlen.CameraSystem
{
    /// <summary>
    /// Lightweight orbit camera: mouse/stick look, smoothed follow, pitch limits and sphere-cast collision
    /// so walls and trees don't block the view. Dependency-free (no Cinemachine) to keep the prototype
    /// small; it can be swapped for a CinemachineCamera later without touching player code, since the
    /// player only reads this transform's yaw.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform target;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Pivot offset from the target's origin (roughly shoulder height).")]
        [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        [Tooltip("Horizontal shoulder offset applied after rotation. Positive = over the right shoulder.")]
        [SerializeField] float shoulderOffset = 0.35f;

        [Header("Orbit")]
        [SerializeField, Min(0.5f)] float distance = 5f;
        [SerializeField] float minPitch = -35f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float startPitch = 15f;

        [Header("Sensitivity")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField, Min(0f)] float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField, Min(0f)] float stickSensitivity = 180f;
        [SerializeField] bool invertY;

        [Header("Smoothing")]
        [Tooltip("Follow lag of the pivot. 0 = locked to the target.")]
        [SerializeField, Min(0f)] float followSmoothTime = 0.06f;
        [Tooltip("How fast the camera returns outward after a collision pulled it in.")]
        [SerializeField, Min(0f)] float distanceRecoverySpeed = 6f;

        [Header("Collision")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField, Min(0.01f)] float collisionRadius = 0.25f;
        [SerializeField, Min(0f)] float minDistance = 0.6f;

        [Header("Cursor")]
        [SerializeField] bool lockCursorOnStart = true;

        float yaw;
        float pitch;
        float currentDistance;
        Vector3 pivot;
        Vector3 pivotVelocity;
        readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                SnapToTarget();
            }
        }

        public float Yaw => yaw;
        public float Pitch => pitch;

        void Start()
        {
            if (input == null && target != null) input = target.GetComponent<PlayerInputReader>();
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
            currentDistance = distance;
            SnapToTarget();
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            pivot = target.position + pivotOffset;
            pivotVelocity = Vector3.zero;
            ApplyTransform(Time.deltaTime, snap: true);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale <= 0f) return; // paused: freeze the view

            ReadLook(dt);
            Vector3 desiredPivot = target.position + pivotOffset;
            pivot = followSmoothTime > 0f
                ? Vector3.SmoothDamp(pivot, desiredPivot, ref pivotVelocity, followSmoothTime, Mathf.Infinity, Time.deltaTime)
                : desiredPivot;
            ApplyTransform(Time.deltaTime, snap: false);
        }

        void ReadLook(float dt)
        {
            if (input == null || Cursor.lockState != CursorLockMode.Locked && input.LookIsPointerDelta) return;

            Vector2 look = input.Look;
            if (input.LookIsPointerDelta) look *= mouseSensitivity;
            else look *= stickSensitivity * dt;

            yaw += look.x;
            pitch += invertY ? look.y : -look.y;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            yaw = Mathf.Repeat(yaw, 360f);
        }

        void ApplyTransform(float dt, bool snap)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 shoulderPivot = pivot + rotation * new Vector3(shoulderOffset, 0f, 0f);
            float allowed = ComputeAllowedDistance(shoulderPivot, rotation * Vector3.back, distance);

            if (snap || allowed < currentDistance) currentDistance = allowed; // pull in instantly
            else currentDistance = Mathf.MoveTowards(currentDistance, allowed, distanceRecoverySpeed * dt);

            Vector3 position = shoulderPivot + rotation * Vector3.back * currentDistance;
            transform.SetPositionAndRotation(position, rotation);
        }

        float ComputeAllowedDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, hitBuffer, maxDistance,
                collisionMask, QueryTriggerInteraction.Ignore);

            float closest = maxDistance;
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (target != null && hit.transform.IsChildOf(target)) continue; // ignore the player itself
                if (hit.distance <= 0f) continue; // sphere started inside a collider; ignore
                if (hit.distance < closest) closest = hit.distance;
            }
            return Mathf.Max(minDistance, closest);
        }

        void OnValidate()
        {
            if (maxPitch < minPitch) maxPitch = minPitch;
            startPitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
        }
    }
}
