using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PlayerTraitProfileTests
    {
        private sealed class MemoryStore : IPlayerTraitStore
        {
            private readonly Dictionary<string, PlayerTraitProgress> accounts =
                new Dictionary<string, PlayerTraitProgress>();

            public PlayerTraitProgress Load(string accountId) =>
                accounts.TryGetValue(accountId, out var progress) ? progress.Copy() : new PlayerTraitProgress();

            public void Save(string accountId, PlayerTraitProgress progress) =>
                accounts[accountId] = progress.Copy();
        }

        [Test]
        public void DifferentAccountsLoadDifferentEffectsAndOnlyLocalOwnerCanSave()
        {
            var store = new MemoryStore();
            var aliceProgress = new PlayerTraitProgress();
            aliceProgress.GrantLevel();
            aliceProgress.GrantLevel();
            aliceProgress.TryInvest("survival.armor");
            aliceProgress.TryInvest("combat.cargo_fire");
            store.Save("alice", aliceProgress);
            var alice = new GameObject("Alice", typeof(ActorHealth), typeof(RangedAttackController),
                typeof(PlayerTraitProfile));
            var bob = new GameObject("Bob", typeof(ActorHealth), typeof(RangedAttackController),
                typeof(PlayerTraitProfile));
            try
            {
                var aliceProfile = alice.GetComponent<PlayerTraitProfile>();
                var bobProfile = bob.GetComponent<PlayerTraitProfile>();
                aliceProfile.BindLocalAccount("alice", store);
                bobProfile.BindLocalAccount("bob", store);
                Assert.That(alice.GetComponent<ActorHealth>().MaxHealth, Is.EqualTo(120f).Within(.001f));
                Assert.That(bob.GetComponent<ActorHealth>().MaxHealth, Is.EqualTo(100f));
                Assert.That(alice.GetComponent<RangedAttackController>().EffectiveDamage,
                    Is.EqualTo(22f).Within(.001f));
                Assert.That(bob.GetComponent<RangedAttackController>().EffectiveDamage, Is.EqualTo(20f));

                Assert.That(aliceProfile.GrantMissionLevel(), Is.True);
                Assert.That(store.Load("alice").level, Is.EqualTo(4));
                Assert.That(store.Load("bob").level, Is.EqualTo(1));
                bobProfile.BindRemoteAccount("bob", aliceProgress);
                Assert.That(bobProfile.GrantMissionLevel(), Is.False);
                Assert.That(store.Load("bob").level, Is.EqualTo(1));
                Assert.That(bobProfile.IsLocalOwner, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(alice);
                Object.DestroyImmediate(bob);
            }
        }
    }
}
