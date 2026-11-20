using Arlen.Core;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class MovementMathTests
    {
        const float Eps = 0.0001f;

        static void AssertVec(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Eps), $"Expected {expected}, got {actual}");
        }

        [Test]
        public void ForwardInputWithZeroYawIsWorldForward()
        {
            AssertVec(Vector3.forward, MovementMath.CameraRelative(Vector2.up, 0f));
        }

        [Test]
        public void ForwardInputFollowsCameraYaw()
        {
            AssertVec(Vector3.right, MovementMath.CameraRelative(Vector2.up, 90f));
            AssertVec(Vector3.back, MovementMath.CameraRelative(Vector2.up, 180f));
        }

        [Test]
        public void RightInputIsCameraRight()
        {
            AssertVec(Vector3.back, MovementMath.CameraRelative(Vector2.right, 90f));
        }

        [Test]
        public void DiagonalInputIsClampedToUnitLength()
        {
            Vector3 v = MovementMath.CameraRelative(new Vector2(1f, 1f), 0f);
            Assert.AreEqual(1f, v.magnitude, Eps);
            Assert.AreEqual(0f, v.y);
        }

        [Test]
        public void JumpVelocityReachesRequestedHeight()
        {
            float g = -20f, h = 1.5f;
            float v = MovementMath.JumpVelocity(h, g);
            // Apex height = v² / (2|g|)
            Assert.AreEqual(h, v * v / (2f * -g), 0.001f);
        }

        [Test]
        public void JumpVelocityIsZeroForInvalidInput()
        {
            Assert.AreEqual(0f, MovementMath.JumpVelocity(1f, 5f));
            Assert.AreEqual(0f, MovementMath.JumpVelocity(0f, -9.8f));
        }

        [Test]
        public void AccelerateUsesDecelerationWhenTargetIsZero()
        {
            Vector3 v = MovementMath.Accelerate(new Vector3(10f, 0f, 0f), Vector3.zero, 1f, 5f, 1f);
            AssertVec(new Vector3(5f, 0f, 0f), v);
        }

        [Test]
        public void CurveAverageOfConstantCurveIsThatConstant()
        {
            var c = AnimationCurve.Constant(0f, 1f, 2f);
            Assert.AreEqual(2f, MovementMath.CurveAverage(c), 0.001f);
            Assert.AreEqual(0.5f, MovementMath.CurveAverage(AnimationCurve.Linear(0f, 0f, 1f, 1f)), 0.01f);
        }
    }
}
