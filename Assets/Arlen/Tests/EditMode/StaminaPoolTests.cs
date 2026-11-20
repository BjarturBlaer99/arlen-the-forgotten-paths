using Arlen.Core;
using NUnit.Framework;

namespace Arlen.Tests
{
    public class StaminaPoolTests
    {
        [Test]
        public void TrySpendRequiresFullAmount()
        {
            var s = new StaminaPool(100f, 10f, 1f);
            Assert.IsTrue(s.TrySpend(70f));
            Assert.AreEqual(30f, s.Current);
            Assert.IsFalse(s.TrySpend(40f), "Not enough stamina should fail.");
            Assert.AreEqual(30f, s.Current, "Failed spend must not consume stamina.");
        }

        [Test]
        public void RegenWaitsForDelay()
        {
            var s = new StaminaPool(100f, 20f, 1f);
            s.TrySpend(50f);
            s.Tick(0.5f);
            Assert.AreEqual(50f, s.Current, "No regen during delay.");
            s.Tick(0.5f);
            Assert.AreEqual(50f, s.Current, 0.001f, "Delay just elapsed; no leftover time.");
            s.Tick(1f);
            Assert.AreEqual(70f, s.Current, 0.001f);
        }

        [Test]
        public void RegenUsesLeftoverTimeAfterDelay()
        {
            var s = new StaminaPool(100f, 10f, 1f);
            s.TrySpend(50f);
            s.Tick(1.5f);
            Assert.AreEqual(55f, s.Current, 0.001f);
        }

        [Test]
        public void RegenClampsToMax()
        {
            var s = new StaminaPool(100f, 1000f, 0f);
            s.TrySpend(10f);
            s.Tick(1f);
            Assert.AreEqual(100f, s.Current);
        }

        [Test]
        public void SpendingResetsRegenDelay()
        {
            var s = new StaminaPool(100f, 10f, 1f);
            s.TrySpend(10f);
            s.Tick(0.9f);
            s.TrySpend(10f);
            s.Tick(0.9f);
            Assert.AreEqual(80f, s.Current, 0.001f);
        }

        [Test]
        public void DrainToZeroExhaustsUntilRecoveryThreshold()
        {
            var s = new StaminaPool(100f, 50f, 0f, exhaustionRecoverFraction: 0.3f);
            int exhausted = 0, recovered = 0;
            s.Exhausted += () => exhausted++;
            s.Recovered += () => recovered++;

            Assert.IsTrue(s.Drain(60f));
            Assert.IsFalse(s.Drain(60f), "Draining past zero stops the action.");
            Assert.AreEqual(0f, s.Current);
            Assert.IsTrue(s.IsExhausted);
            Assert.AreEqual(1, exhausted);

            s.Tick(0.4f); // 20 stamina, below 30% threshold
            Assert.IsTrue(s.IsExhausted);
            Assert.IsFalse(s.Drain(1f), "Cannot sprint while exhausted.");

            s.Tick(0.4f); // 40 stamina
            Assert.IsFalse(s.IsExhausted);
            Assert.AreEqual(1, recovered);
            Assert.IsTrue(s.Drain(1f));
        }

        [Test]
        public void TrySpendStillWorksWhileExhaustedIfEnoughStamina()
        {
            // Exhaustion only gates continuous drain (sprint); a dodge with enough stamina is allowed.
            var s = new StaminaPool(100f, 100f, 0f, 0.9f);
            s.Drain(200f);
            s.Tick(0.3f);
            Assert.IsTrue(s.IsExhausted);
            Assert.IsTrue(s.TrySpend(20f));
        }

        [Test]
        public void RefillClearsExhaustion()
        {
            var s = new StaminaPool(100f, 0f, 0f);
            s.Drain(500f);
            s.Refill();
            Assert.AreEqual(100f, s.Current);
            Assert.IsFalse(s.IsExhausted);
        }
    }
}
