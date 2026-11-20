using Arlen.Combat;
using Arlen.Player;
using NUnit.Framework;
using UnityEngine;

namespace Arlen.Tests
{
    public class DamagePipelineTests
    {
        class FakeInterceptor : IDamageInterceptor
        {
            public DamageResult Result = DamageResult.Applied;
            public int Calls;
            public DamageResult Intercept(in DamageInfo info) { Calls++; return Result; }
        }

        GameObject go;
        HealthComponent health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DamageTest");
            health = go.AddComponent<HealthComponent>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        static DamageInfo Hit(float amount, bool parryable = true) =>
            new DamageInfo(amount, null, Vector3.zero, Vector3.forward, parryable);

        [Test]
        public void PlainHitIsApplied()
        {
            Assert.AreEqual(DamageResult.Applied, health.ApplyDamage(Hit(10f)));
            Assert.AreEqual(health.Max - 10f, health.Current);
        }

        [Test]
        public void InvulnerableIgnoresAndRaisesAvoided()
        {
            bool avoided = false;
            health.DamageAvoided += _ => avoided = true;
            health.AddInvulnerability();
            Assert.AreEqual(DamageResult.Ignored, health.ApplyDamage(Hit(10f)));
            Assert.IsTrue(avoided);
            health.RemoveInvulnerability();
            Assert.AreEqual(DamageResult.Applied, health.ApplyDamage(Hit(10f)));
        }

        [Test]
        public void InvulnerabilityIsReferenceCounted()
        {
            health.AddInvulnerability();
            health.AddInvulnerability();
            health.RemoveInvulnerability();
            Assert.IsTrue(health.IsInvulnerable);
            health.RemoveInvulnerability();
            Assert.IsFalse(health.IsInvulnerable);
        }

        [Test]
        public void InterceptorCanParry()
        {
            var parry = new FakeInterceptor { Result = DamageResult.Parried };
            bool parriedEvent = false, damagedEvent = false;
            health.DamageParried += _ => parriedEvent = true;
            health.Damaged += _ => damagedEvent = true;
            health.AddInterceptor(parry);

            Assert.AreEqual(DamageResult.Parried, health.ApplyDamage(Hit(30f)));
            Assert.AreEqual(health.Max, health.Current);
            Assert.IsTrue(parriedEvent);
            Assert.IsFalse(damagedEvent);
        }

        [Test]
        public void PassThroughInterceptorLetsDamageApply()
        {
            var probe = new FakeInterceptor();
            health.AddInterceptor(probe);
            Assert.AreEqual(DamageResult.Applied, health.ApplyDamage(Hit(5f)));
            Assert.AreEqual(1, probe.Calls);
        }

        [Test]
        public void RemovedInterceptorIsNotCalled()
        {
            var parry = new FakeInterceptor { Result = DamageResult.Parried };
            health.AddInterceptor(parry);
            health.RemoveInterceptor(parry);
            Assert.AreEqual(DamageResult.Applied, health.ApplyDamage(Hit(5f)));
            Assert.AreEqual(0, parry.Calls);
        }

        [Test]
        public void DeadTargetsIgnoreDamage()
        {
            health.ApplyDamage(Hit(1000f));
            Assert.AreEqual(DamageResult.Ignored, health.ApplyDamage(Hit(5f)));
        }

        [Test]
        public void TeamsHostility()
        {
            Assert.IsTrue(Targetable.AreHostile(Team.Player, Team.Enemy));
            Assert.IsFalse(Targetable.AreHostile(Team.Enemy, Team.Enemy));
            Assert.IsFalse(Targetable.AreHostile(Team.Neutral, Team.Enemy));
        }

        [Test]
        public void ForceBeginInterruptsNonCancelableAction()
        {
            var state = go.AddComponent<PlayerActionState>();
            PlayerAction interrupted = PlayerAction.None;
            state.Interrupted += a => interrupted = a;
            state.TryBegin(PlayerAction.Attack);
            state.ForceBegin(PlayerAction.HitStun);
            Assert.AreEqual(PlayerAction.HitStun, state.Current);
            Assert.AreEqual(PlayerAction.Attack, interrupted);
        }
    }
}
