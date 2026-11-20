using Arlen.Core;
using NUnit.Framework;

namespace Arlen.Tests
{
    public class HealthPoolTests
    {
        [Test]
        public void StartsAtMax()
        {
            var h = new HealthPool(100f);
            Assert.AreEqual(100f, h.Current);
            Assert.IsFalse(h.IsDead);
        }

        [Test]
        public void DamageReducesHealthAndClampsAtZero()
        {
            var h = new HealthPool(50f);
            Assert.AreEqual(20f, h.TakeDamage(20f));
            Assert.AreEqual(30f, h.Current);
            Assert.AreEqual(30f, h.TakeDamage(999f), "Only remaining health is removed.");
            Assert.AreEqual(0f, h.Current);
            Assert.IsTrue(h.IsDead);
        }

        [Test]
        public void DiedRaisedExactlyOnce()
        {
            var h = new HealthPool(10f);
            int died = 0;
            h.Died += () => died++;
            h.TakeDamage(10f);
            h.TakeDamage(10f);
            Assert.AreEqual(1, died);
        }

        [Test]
        public void NegativeOrZeroDamageIsIgnored()
        {
            var h = new HealthPool(10f);
            Assert.AreEqual(0f, h.TakeDamage(-5f));
            Assert.AreEqual(0f, h.TakeDamage(0f));
            Assert.AreEqual(10f, h.Current);
        }

        [Test]
        public void HealClampsToMaxAndCannotHealDead()
        {
            var h = new HealthPool(100f);
            h.TakeDamage(30f);
            Assert.AreEqual(30f, h.Heal(50f));
            Assert.AreEqual(100f, h.Current);

            h.TakeDamage(100f);
            Assert.AreEqual(0f, h.Heal(10f));
            Assert.IsTrue(h.IsDead);
        }

        [Test]
        public void ReviveRestoresAndRaisesEvent()
        {
            var h = new HealthPool(80f);
            bool revived = false;
            h.Revived += () => revived = true;
            h.TakeDamage(80f);
            h.Revive();
            Assert.IsTrue(revived);
            Assert.AreEqual(80f, h.Current);
        }

        [Test]
        public void ChangedReportsCurrentAndMax()
        {
            var h = new HealthPool(40f);
            float c = -1f, m = -1f;
            h.Changed += (cur, max) => { c = cur; m = max; };
            h.TakeDamage(15f);
            Assert.AreEqual(25f, c);
            Assert.AreEqual(40f, m);
        }

        [Test]
        public void SetMaxKeepsRatio()
        {
            var h = new HealthPool(100f);
            h.TakeDamage(50f);
            h.SetMax(200f);
            Assert.AreEqual(100f, h.Current, 0.001f);
        }
    }
}
