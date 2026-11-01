using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class CombatContractTests
    {
        private GameObject actor;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Combat contract actor");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(actor);
        }

        [Test]
        public void ApplyDamage_ClampsHealthRaisesDeathOnceAndIgnoresLaterDamage()
        {
            var health = actor.AddComponent<ActorHealth>();
            var deaths = 0;
            health.Died += () => deaths++;

            health.ApplyDamage(25f);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Assert.That(health.IsDead, Is.False);
            Assert.That(deaths, Is.Zero);

            health.ApplyDamage(100f);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));

            health.ApplyDamage(20f);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ApplyDamage_InvalidDamage_RejectsWithoutChangingHealth(float damage)
        {
            var health = actor.AddComponent<ActorHealth>();
            Assert.Throws<ArgumentOutOfRangeException>(() => health.ApplyDamage(damage));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        }

        [Test]
        public void Apply_StunExtendsButNeverShortensActiveStun()
        {
            var stun = actor.AddComponent<StunState>();
            var changes = new List<bool>();
            stun.StunChanged += changes.Add;

            stun.Apply(5f, 10f);
            Assert.That(stun.IsStunned, Is.True);
            Assert.That(stun.StunnedUntil, Is.EqualTo(15f));
            stun.Apply(1f, 11f);
            Assert.That(stun.StunnedUntil, Is.EqualTo(15f));
            stun.Apply(8f, 12f);
            Assert.That(stun.StunnedUntil, Is.EqualTo(20f));
            Assert.That(changes, Is.EqualTo(new[] { true }));

            stun.Tick(19f);
            Assert.That(stun.IsStunned, Is.True);
            stun.Tick(20f);
            stun.Tick(21f);
            Assert.That(stun.IsStunned, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }

        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(1f, -1f)]
        [TestCase(1f, float.NaN)]
        public void Apply_InvalidStunTiming_RejectsInput(float duration, float now)
        {
            var stun = actor.AddComponent<StunState>();
            Assert.Throws<ArgumentOutOfRangeException>(() => stun.Apply(duration, now));
            Assert.That(stun.IsStunned, Is.False);
        }

        [Test]
        public void Apply_ZeroDuration_DoesNotStartStun()
        {
            var stun = actor.AddComponent<StunState>();
            var changes = 0;
            stun.StunChanged += _ => changes++;
            stun.Apply(0f, 10f);
            Assert.That(stun.IsStunned, Is.False);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void RevealTag_RaisesOnlyNewlyRevealedTags()
        {
            var observable = actor.AddComponent<ObservableActor>();
            var observed = new List<ObservedCombatTag>();
            observable.TagObserved += observed.Add;

            observable.RevealTag(ObservedCombatTag.None);
            observable.RevealTag(ObservedCombatTag.Shield);
            observable.RevealTag(ObservedCombatTag.Shield);
            observable.RevealTag(ObservedCombatTag.Shield | ObservedCombatTag.Healing);

            Assert.That(observable.ObservedTags, Is.EqualTo(ObservedCombatTag.Shield | ObservedCombatTag.Healing));
            Assert.That(observed, Is.EqualTo(new[] { ObservedCombatTag.Shield, ObservedCombatTag.Healing }));
        }

        [Test]
        public void ActorId_IsPositiveUniqueAndStableAcrossDisableEnable()
        {
            var observable = actor.AddComponent<ObservableActor>();
            var other = new GameObject("Other actor");
            try
            {
                var otherObservable = other.AddComponent<ObservableActor>();
                var id = observable.ActorId;
                Assert.That(id, Is.GreaterThan(0));
                Assert.That(otherObservable.ActorId, Is.GreaterThan(0));
                Assert.That(otherObservable.ActorId, Is.Not.EqualTo(id));
                actor.SetActive(false);
                actor.SetActive(true);
                Assert.That(observable.ActorId, Is.EqualTo(id));
                Assert.That(observable.Velocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
            }
        }
    }
}
