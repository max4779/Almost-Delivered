using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay.Support;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PlayerSupportTests
    {
        private sealed class MemoryStore : IPlayerSupportStore
        {
            private readonly Dictionary<string, int> values = new Dictionary<string, int>();
            public int Load(string accountId) => values.TryGetValue(accountId, out var value) ? value : 30;
            public void Save(string accountId, int credits) => values[accountId] = credits;
        }

        [Test]
        public void CreditsAreSpentAndRestoredPerAccount()
        {
            var store = new MemoryStore();
            var first = new PlayerSupportWallet("crew-a", store);
            var second = new PlayerSupportWallet("crew-b", store);
            Assert.That(first.TrySpend(15), Is.True);
            Assert.That(first.Credits, Is.EqualTo(15));
            Assert.That(second.Credits, Is.EqualTo(30));
            Assert.That(first.Grant(20), Is.True);
            Assert.That(new PlayerSupportWallet("crew-a", store).Credits, Is.EqualTo(35));
            Assert.That(first.TrySpend(36), Is.False);
        }

        [Test]
        public void ShieldAbsorbsDamageBeforePlayerHealth()
        {
            var actor = new GameObject("Shield Test Actor");
            try
            {
                var health = actor.AddComponent<ActorHealth>();
                health.SetShield(45f);
                health.ApplyDamage(30f);
                Assert.That(health.CurrentShield, Is.EqualTo(15f));
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
                health.ApplyDamage(25f);
                Assert.That(health.CurrentShield, Is.Zero);
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 10f));
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
