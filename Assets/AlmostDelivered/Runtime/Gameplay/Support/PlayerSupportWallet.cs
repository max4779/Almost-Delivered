using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Support
{
    public interface IPlayerSupportStore
    {
        int Load(string accountId);
        void Save(string accountId, int credits);
    }

    /// <summary>Prototype account storage. The online service can replace this store without changing abilities.</summary>
    public sealed class LocalPlayerSupportStore : IPlayerSupportStore
    {
        private const string Prefix = "AlmostDelivered.SupportCredits.v1.";
        public int Load(string accountId) => Mathf.Max(0, PlayerPrefs.GetInt(Key(accountId), 30));
        public void Save(string accountId, int credits)
        {
            PlayerPrefs.SetInt(Key(accountId), Mathf.Max(0, credits));
            PlayerPrefs.Save();
        }

        private static string Key(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.");
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(accountId));
                return Prefix + BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }

    public sealed class PlayerSupportWallet
    {
        private readonly IPlayerSupportStore store;
        public string AccountId { get; }
        public int Credits { get; private set; }
        public event Action<int> Changed;

        public PlayerSupportWallet(string accountId, IPlayerSupportStore accountStore)
        {
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.");
            store = accountStore ?? throw new ArgumentNullException(nameof(accountStore));
            AccountId = accountId;
            Credits = Mathf.Max(0, store.Load(accountId));
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Credits < amount) return false;
            Credits -= amount;
            Commit();
            return true;
        }

        public bool Grant(int amount)
        {
            if (amount <= 0) return false;
            Credits = Credits > int.MaxValue - amount ? int.MaxValue : Credits + amount;
            Commit();
            return true;
        }

        private void Commit()
        {
            store.Save(AccountId, Credits);
            Changed?.Invoke(Credits);
        }
    }
}
