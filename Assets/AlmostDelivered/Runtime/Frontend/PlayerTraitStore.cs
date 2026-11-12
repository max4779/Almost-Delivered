using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public interface IPlayerTraitStore
    {
        PlayerTraitProgress Load(string accountId);
        void Save(string accountId, PlayerTraitProgress progress);
    }

    /// <summary>Prototype device storage. Replace with an authenticated service for online accounts.</summary>
    public sealed class LocalPlayerTraitStore : IPlayerTraitStore
    {
        private const string Prefix = "AlmostDelivered.Traits.v1.";

        public PlayerTraitProgress Load(string accountId)
        {
            var json = PlayerPrefs.GetString(Key(accountId), string.Empty);
            if (string.IsNullOrEmpty(json)) return new PlayerTraitProgress();
            try { return JsonUtility.FromJson<PlayerTraitProgress>(json) ?? new PlayerTraitProgress(); }
            catch (Exception) { return new PlayerTraitProgress(); }
        }

        public void Save(string accountId, PlayerTraitProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            PlayerPrefs.SetString(Key(accountId), JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }

        private static string Key(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(accountId));
                return Prefix + BitConverter.ToString(bytes).Replace("-", string.Empty);
            }
        }
    }

    public static class LocalPlayerAccount
    {
        private const string GuestKey = "AlmostDelivered.LocalGuestId.v1";
        private const string CloudGuestKey = "AlmostDelivered.CloudGuestId.v1";
        private static string currentAccountId;

        public static string CurrentAccountId
        {
            get
            {
                if (!string.IsNullOrEmpty(currentAccountId)) return currentAccountId;
                if (FrontendAccountSession.IsSignedIn)
                    return currentAccountId = FrontendAccountSession.AccountId;
                var guestId = PlayerPrefs.GetString(CloudGuestKey, string.Empty);
                if (string.IsNullOrWhiteSpace(guestId))
                    guestId = PlayerPrefs.GetString(GuestKey, string.Empty);
                if (string.IsNullOrWhiteSpace(guestId))
                {
                    guestId = "guest:" + Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(GuestKey, guestId);
                    PlayerPrefs.Save();
                }
                return currentAccountId = guestId;
            }
        }

        /// <summary>Call after authentication, before entering the hub.</summary>
        public static void UseAuthenticatedAccount(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            currentAccountId = accountId;
        }

        public static void UseGuestAccount()
        {
            currentAccountId = null;
        }

        public static void UseCloudGuestAccount(string accountId)
        {
            UseAuthenticatedAccount(accountId);
            PlayerPrefs.SetString(CloudGuestKey, accountId);
            PlayerPrefs.Save();
        }
    }
}
