using UnityEngine;

namespace Arlen.Core
{
    /// <summary>Pure helpers for locomotion maths, kept static so they can be unit-tested.</summary>
    public static class MovementMath
    {
        /// <summary>
        /// Converts a 2D input (x = right, y = forward) into a world-space direction on the XZ plane,
        /// relative to the given camera yaw in degrees. Magnitude of the input is preserved (clamped to 1).
        /// </summary>
        public static Vector3 CameraRelative(Vector2 input, float cameraYawDegrees)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            Quaternion yaw = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            Vector3 world = yaw * new Vector3(input.x, 0f, input.y);
            world.y = 0f;
            return world;
        }

        /// <summary>Initial upward velocity needed to reach <paramref name="height"/> under <paramref name="gravity"/> (negative).</summary>
        public static float JumpVelocity(float height, float gravity)
        {
            if (height <= 0f || gravity >= 0f) return 0f;
            return Mathf.Sqrt(2f * height * -gravity);
        }

        /// <summary>Moves a planar velocity toward a target with separate acceleration/deceleration rates.</summary>
        public static Vector3 Accelerate(Vector3 current, Vector3 target, float acceleration, float deceleration, float deltaTime)
        {
            float rate = target.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            return Vector3.MoveTowards(current, target, rate * deltaTime);
        }

        /// <summary>
        /// Average value of a curve over [0,1], used to scale a speed curve so a dodge covers an exact distance.
        /// </summary>
        public static float CurveAverage(AnimationCurve curve, int samples = 64)
        {
            if (curve == null || curve.length == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < samples; i++)
                sum += curve.Evaluate((i + 0.5f) / samples);
            float avg = sum / samples;
            return avg > 0.0001f ? avg : 1f;
        }
    }
}
