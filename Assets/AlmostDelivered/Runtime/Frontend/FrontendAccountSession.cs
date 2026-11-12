using UnityEngine;

namespace AlmostDelivered.Frontend
{
    /// <summary>Development-only local session for the SSH-tunnel API.</summary>
    public static class FrontendAccountSession
    {
        public const string ApiUrl = "http://127.0.0.1:15001";
        private const string AccountIdKey = "AlmostDelivered.AccountId.v1";
        private const string AccountTokenKey = "AlmostDelivered.AccountToken.v1";
        public const string GuestTokenKey = "AlmostDelivered.CloudGuestToken.v1";

        public static string AccountId => PlayerPrefs.GetString(AccountIdKey, string.Empty);
        public static string AccountToken => PlayerPrefs.GetString(AccountTokenKey, string.Empty);
        public static bool IsSignedIn => !string.IsNullOrEmpty(AccountId) && !string.IsNullOrEmpty(AccountToken);
        public static string GuestToken => PlayerPrefs.GetString(GuestTokenKey, string.Empty);

        public static void Remember(string accountId, string token, PlayerTraitProgress progress)
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(token) || progress == null)
                throw new System.ArgumentException("Account reply is incomplete.");
            PlayerPrefs.SetString(AccountIdKey, accountId);
            PlayerPrefs.SetString(AccountTokenKey, token);
            PlayerPrefs.SetInt("AlmostDelivered.CloudPendingSave.v1", 0);
            PlayerPrefs.Save();
            new LocalPlayerTraitStore().Save(accountId, progress);
            LocalPlayerAccount.UseAuthenticatedAccount(accountId);
            RemoteTraitSync.ResetAfterAccountChange();
        }

        public static void Forget()
        {
            PlayerPrefs.DeleteKey(AccountIdKey);
            PlayerPrefs.DeleteKey(AccountTokenKey);
            PlayerPrefs.SetInt("AlmostDelivered.CloudPendingSave.v1", 0);
            PlayerPrefs.Save();
            LocalPlayerAccount.UseGuestAccount();
            RemoteTraitSync.ResetAfterAccountChange();
        }
    }
}
