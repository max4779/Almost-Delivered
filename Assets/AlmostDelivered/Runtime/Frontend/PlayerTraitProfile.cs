using System;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Frontend
{
    /// <summary>One player's traits. Network code must bind each spawned player to its authenticated account.</summary>
    public sealed class PlayerTraitProfile : MonoBehaviour
    {
        private IPlayerTraitStore store;
        private PlayerTraitProgress progress;
        private float baseMaximumHealth;
        private bool isLocalOwner;

        public string AccountId { get; private set; }
        public bool IsLocalOwner => isLocalOwner;
        public PlayerTraitProgress Progress => progress;
        public event Action Changed;
        public event Action<PlayerTraitProgress> Committed;

        private void Awake()
        {
            var health = GetComponent<ActorHealth>();
            baseMaximumHealth = health != null ? health.MaxHealth : 100f;
        }

        public void EnsureLocalAccount()
        {
            if (progress == null) BindLocalAccount(LocalPlayerAccount.CurrentAccountId);
        }

        public void BindLocalAccount(string accountId, IPlayerTraitStore accountStore = null)
        {
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            AccountId = accountId;
            isLocalOwner = true;
            store = accountStore ?? new LocalPlayerTraitStore();
            var loaded = store.Load(accountId);
            progress = Sanitize(loaded);
            if (loaded != null && loaded.SpentPoints != progress.SpentPoints)
                store.Save(accountId, progress.Copy());
            ApplyModifiers();
            Changed?.Invoke();
        }

        public void BindRemoteAccount(string accountId, PlayerTraitProgress replicatedProgress)
        {
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            AccountId = accountId;
            isLocalOwner = false;
            store = null;
            progress = Sanitize(replicatedProgress);
            ApplyModifiers();
            Changed?.Invoke();
        }

        public void ApplyServerSnapshot(string accountId, PlayerTraitProgress serverProgress)
        {
            if (!isLocalOwner || string.IsNullOrWhiteSpace(accountId)) return;
            AccountId = accountId;
            progress = Sanitize(serverProgress);
            store = new LocalPlayerTraitStore();
            store.Save(accountId, progress.Copy());
            ApplyModifiers();
            Changed?.Invoke();
        }

        public bool TryInvest(string id)
        {
            if (!CanEditInHub() || !progress.TryInvest(id)) return false;
            Commit();
            return true;
        }

        public bool ResetInHub()
        {
            if (!CanEditInHub() || progress.SpentPoints == 0) return false;
            progress.ResetRanks();
            Commit();
            return true;
        }

        /// <summary>Prototype reward hook. An online server must award levels and send an authoritative snapshot.</summary>
        public bool GrantMissionLevel()
        {
            if (!isLocalOwner || progress == null || !progress.GrantLevel()) return false;
            Commit();
            return true;
        }

        public float EscortSpeedMultiplier => GrowthMultiplier("escort.lead");
        public float RepairMultiplier => GrowthMultiplier("escort.repair");

        public void ApplyModifiers()
        {
            if (progress == null) return;
            var health = GetComponent<ActorHealth>();
            if (health != null)
                health.ConfigureMaximum(baseMaximumHealth * GrowthMultiplier("survival.armor"),
                    preserveRatio: true);
            GetComponent<RangedAttackController>()?.SetTraitModifiers(
                GrowthMultiplier("combat.cargo_fire"),
                2f - GrowthMultiplier("combat.counter"));
            GetComponent<MeleeAttackController>()?.SetTraitModifiers(
                GrowthMultiplier("combat.cargo_fire"),
                2f - GrowthMultiplier("combat.counter"));
            GetComponent<PlayerMobility>()?.SetTraitRecoveryMultiplier(
                GrowthMultiplier("survival.mobility"));
        }

        private float GrowthMultiplier(string id)
        {
            var rank = progress != null ? progress.Rank(id) : 0;
            var definition = PlayerTraitCatalog.Find(id);
            return rank == 0 || definition == null ? 1f :
                1f + definition.FirstRankBonus + (rank - 1) * definition.LaterRankBonus;
        }

        private bool CanEditInHub() => isLocalOwner && progress != null &&
            SceneManager.GetActiveScene().name == GameSceneFlow.DepartureHubScene;

        private void Commit()
        {
            store.Save(AccountId, progress.Copy());
            ApplyModifiers();
            Changed?.Invoke();
            Committed?.Invoke(progress.Copy());
        }

        private static PlayerTraitProgress Sanitize(PlayerTraitProgress source)
        {
            var clean = new PlayerTraitProgress();
            if (source == null) return clean;
            clean.level = Mathf.Clamp(source.level, 1, PlayerTraitCatalog.TotalPointLimit);
            foreach (var definition in PlayerTraitCatalog.All)
            {
                var rank = source.Rank(definition.Id);
                for (var i = 0; i < rank && clean.TryInvest(definition.Id); i++) { }
            }
            return clean;
        }
    }
}
