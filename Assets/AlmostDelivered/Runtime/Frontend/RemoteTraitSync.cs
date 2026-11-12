using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AlmostDelivered.Frontend
{
    /// <summary>Local development bridge: Unity -> SSH tunnel -> private API -> MySQL.</summary>
    public sealed class RemoteTraitSync : MonoBehaviour
    {
        private const string BaseUrl = FrontendAccountSession.ApiUrl;
        private const string TokenKey = FrontendAccountSession.GuestTokenKey;
        private const string PendingKey = "AlmostDelivered.CloudPendingSave.v1";
        private static RemoteTraitSync instance;

        [Serializable]
        private sealed class ProfileReply
        {
            public string accountId;
            public string token;
            public PlayerTraitProgress progress;
        }

        private PlayerTraitProfile profile;
        private Coroutine operation;
        private bool pendingSave;
        private int revision;
        private float retryAt;
        private string status = "서버 연결 대기 · 기기 저장";

        public static string Status => instance != null ? instance.status : "기기 저장";

        public static void ResetAfterAccountChange()
        {
            if (instance == null) return;
            if (instance.operation != null) instance.StopCoroutine(instance.operation);
            if (instance.profile != null) instance.profile.Committed -= instance.OnCommitted;
            instance.profile = null;
            instance.operation = null;
            instance.pendingSave = false;
            instance.retryAt = 0f;
            instance.status = "서버 연결 대기 · 기기 저장";
        }

        public static RemoteTraitSync Ensure()
        {
            if (instance != null) return instance;
            var holder = new GameObject("Almost Delivered Profile Sync");
            DontDestroyOnLoad(holder);
            return holder.AddComponent<RemoteTraitSync>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            pendingSave = PlayerPrefs.GetInt(PendingKey, 0) == 1;
        }

        public void Attach(PlayerTraitProfile localProfile)
        {
            if (profile != null) profile.Committed -= OnCommitted;
            if (operation != null) { StopCoroutine(operation); operation = null; }
            profile = localProfile;
            if (profile == null || !profile.IsLocalOwner) return;
            profile.Committed += OnCommitted;
            retryAt = 0f;
        }

        private void Update()
        {
            if (profile != null && operation == null && Time.unscaledTime >= retryAt)
                operation = StartCoroutine(Synchronize());
        }

        private void OnCommitted(PlayerTraitProgress unused)
        {
            revision++;
            pendingSave = true;
            PlayerPrefs.SetInt(PendingKey, 1);
            PlayerPrefs.Save();
            status = "서버 저장 중 · 기기에도 저장됨";
            if (operation == null) retryAt = 0f;
        }

        private IEnumerator Synchronize()
        {
            var signedIn = FrontendAccountSession.IsSignedIn;
            var token = signedIn ? FrontendAccountSession.AccountToken : PlayerPrefs.GetString(TokenKey, string.Empty);
            if (string.IsNullOrEmpty(token))
            {
                var sentRevision = revision;
                var local = profile.Progress.Copy();
                using (var request = JsonRequest("POST", "/v1/profiles/guest", local, null))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Failed(request);
                        yield break;
                    }
                    var reply = JsonUtility.FromJson<ProfileReply>(request.downloadHandler.text);
                    if (reply == null || string.IsNullOrWhiteSpace(reply.token) ||
                        string.IsNullOrWhiteSpace(reply.accountId))
                    {
                        Failed(null);
                        yield break;
                    }
                    token = reply.token;
                    PlayerPrefs.SetString(TokenKey, token);
                    LocalPlayerAccount.UseCloudGuestAccount(reply.accountId);
                    if (profile != null)
                        profile.ApplyServerSnapshot(reply.accountId, pendingSave ? profile.Progress : reply.progress);
                }
                if (revision == sentRevision)
                {
                    pendingSave = false;
                    PlayerPrefs.SetInt(PendingKey, 0);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                using (var request = UnityWebRequest.Get(BaseUrl + "/v1/profile"))
                {
                    request.timeout = 5;
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Failed(request);
                        yield break;
                    }
                    var reply = JsonUtility.FromJson<ProfileReply>(request.downloadHandler.text);
                    if (reply == null || string.IsNullOrWhiteSpace(reply.accountId) || reply.progress == null)
                    {
                        Failed(null);
                        yield break;
                    }
                    if (signedIn)
                        LocalPlayerAccount.UseAuthenticatedAccount(reply.accountId);
                    else
                        LocalPlayerAccount.UseCloudGuestAccount(reply.accountId);
                    if (profile != null && !pendingSave)
                        profile.ApplyServerSnapshot(reply.accountId, reply.progress);
                }
            }

            if (pendingSave && profile != null)
            {
                var sentRevision = revision;
                var snapshot = profile.Progress.Copy();
                using (var request = JsonRequest("PUT", "/v1/profile", snapshot, token))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Failed(request);
                        yield break;
                    }
                }
                if (revision == sentRevision)
                {
                    pendingSave = false;
                    PlayerPrefs.SetInt(PendingKey, 0);
                    PlayerPrefs.Save();
                }
            }
            status = pendingSave ? "서버 저장 중 · 기기에도 저장됨" : "서버 연결됨 · 저장 완료";
            retryAt = pendingSave ? 0f : float.PositiveInfinity;
            operation = null;
        }

        private static UnityWebRequest JsonRequest(string method, string path,
            PlayerTraitProgress progress, string token)
        {
            var request = new UnityWebRequest(BaseUrl + path, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(progress)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 5;
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", "Bearer " + token);
            return request;
        }

        private void Failed(UnityWebRequest request)
        {
            status = request != null && request.responseCode == 401
                ? "서버 계정 확인 실패 · 기기 저장"
                : "서버 연결 대기 · 기기 저장";
            retryAt = Time.unscaledTime + 10f;
            operation = null;
        }

        private void OnDestroy()
        {
            if (profile != null) profile.Committed -= OnCommitted;
            if (instance == this) instance = null;
        }
    }
}
