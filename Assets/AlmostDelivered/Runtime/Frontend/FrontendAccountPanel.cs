using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    public sealed class FrontendAccountPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class Credentials
        {
            public string email;
            public string password;
            public string guestToken;
        }

        [Serializable]
        private sealed class AuthReply
        {
            public string accountId;
            public string token;
            public PlayerTraitProgress progress;
        }

        [SerializeField] private GameObject overlay;
        [SerializeField] private InputField emailInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Text heading;
        [SerializeField] private Text message;
        [SerializeField] private Text accountStatus;
        [SerializeField] private Text submitLabel;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button signOutButton;

        private bool registering;
        private bool busy;

        public void Configure(GameObject panel, InputField email, InputField password,
            Text title, Text feedback, Text status, Text actionLabel, Button action, Button logout)
        {
            overlay = panel;
            emailInput = email;
            passwordInput = password;
            heading = title;
            message = feedback;
            accountStatus = status;
            submitLabel = actionLabel;
            submitButton = action;
            signOutButton = logout;
            RefreshAccountStatus();
            overlay.SetActive(false);
        }

        private void Start() => RefreshAccountStatus();

        public void OpenLogin()
        {
            registering = false;
            Open();
        }

        public void OpenRegister()
        {
            registering = true;
            Open();
        }

        private void Open()
        {
            if (busy) return;
            overlay.SetActive(true);
            passwordInput.text = string.Empty;
            message.text = "";
            heading.text = registering ? "승무원 등록" : "승무원 로그인";
            submitLabel.text = registering ? "계정 만들고 시작" : "로그인하고 시작";
            emailInput.ActivateInputField();
        }

        public void Close()
        {
            if (busy) return;
            passwordInput.text = string.Empty;
            overlay.SetActive(false);
        }

        public void Submit()
        {
            if (busy) return;
            var email = emailInput.text.Trim();
            var password = passwordInput.text;
            if (email.Length == 0 || password.Length == 0)
            {
                message.text = "이메일과 비밀번호를 입력해 주세요.";
                return;
            }
            if (registering && password.Length < 10)
            {
                message.text = "비밀번호는 10자 이상으로 설정해 주세요.";
                return;
            }
            StartCoroutine(SendCredentials(email, password));
        }

        private IEnumerator SendCredentials(string email, string password)
        {
            busy = true;
            submitButton.interactable = false;
            message.text = registering ? "계정을 만드는 중..." : "로그인 중...";
            var payload = new Credentials
            {
                email = email,
                password = password,
                guestToken = registering ? FrontendAccountSession.GuestToken : null
            };
            var path = registering ? "/v1/auth/register" : "/v1/auth/login";
            using (var request = new UnityWebRequest(FrontendAccountSession.ApiUrl + path, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 8;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    message.text = request.responseCode == 409 ? "이미 등록된 이메일입니다." :
                        request.responseCode == 401 ? "이메일 또는 비밀번호를 확인해 주세요." :
                        request.responseCode == 400 ? "입력 형식을 확인해 주세요." :
                        "서버에 연결할 수 없습니다. 연결 상태를 확인해 주세요.";
                    busy = false;
                    submitButton.interactable = true;
                    passwordInput.text = string.Empty;
                    yield break;
                }
                var reply = JsonUtility.FromJson<AuthReply>(request.downloadHandler.text);
                if (reply == null || string.IsNullOrWhiteSpace(reply.accountId) ||
                    string.IsNullOrWhiteSpace(reply.token) || reply.progress == null)
                {
                    message.text = "서버 응답을 읽을 수 없습니다.";
                    busy = false;
                    submitButton.interactable = true;
                    yield break;
                }
                FrontendAccountSession.Remember(reply.accountId, reply.token, reply.progress);
            }
            passwordInput.text = string.Empty;
            RefreshAccountStatus();
            busy = false;
            submitButton.interactable = true;
            GameSceneFlow.LoadDepartureHub();
        }

        public void SignOut()
        {
            if (!FrontendAccountSession.IsSignedIn) return;
            var token = FrontendAccountSession.AccountToken;
            FrontendAccountSession.Forget();
            RefreshAccountStatus();
            StartCoroutine(RevokeSession(token));
        }

        private IEnumerator RevokeSession(string token)
        {
            using (var request = new UnityWebRequest(FrontendAccountSession.ApiUrl + "/v1/auth/logout", "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = 5;
                yield return request.SendWebRequest();
            }
        }

        private void RefreshAccountStatus()
        {
            if (accountStatus == null || signOutButton == null) return;
            accountStatus.text = FrontendAccountSession.IsSignedIn
                ? "계정 연결됨 · 계속 플레이할 수 있습니다" : "게스트로 시작하거나 계정에 로그인하세요";
            signOutButton.gameObject.SetActive(FrontendAccountSession.IsSignedIn);
        }
    }
}
