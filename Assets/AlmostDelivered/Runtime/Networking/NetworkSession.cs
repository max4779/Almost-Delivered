using AlmostDelivered.Frontend;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Networking
{
    /// <summary>Prototype session. One Unity server owns scene changes; account storage follows later.</summary>
    public sealed class NetworkSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7777;
        private const string AvatarResource = "Network/NetworkPlayerAvatar";

        private NetworkManager manager;
        private UnityTransport transport;
        private string status = "연결되지 않음";
        private bool disconnecting;

        public static NetworkSession Instance { get; private set; }
        public static bool IsConnected => Instance != null && Instance.manager != null && Instance.manager.IsListening;
        public static bool IsClientOnly => IsConnected && !Instance.manager.IsServer;
        public string Status => status;
        public int PlayerCount => manager != null && manager.IsServer ? manager.ConnectedClientsIds.Count : 0;

        public static NetworkSession Ensure()
        {
            if (Instance != null) return Instance;
            var holder = new GameObject("Almost Delivered Network Session");
            DontDestroyOnLoad(holder);
            return holder.AddComponent<NetworkSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transport = gameObject.AddComponent<UnityTransport>();
            manager = gameObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.PlayerPrefab = Resources.Load<GameObject>(AvatarResource);
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public bool StartHost(ushort port = DefaultPort)
        {
            if (!CanStart()) return false;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (!manager.StartHost())
            {
                status = "서버 시작 실패";
                return false;
            }
            status = "호스트 실행 중 · 포트 " + port;
            LoadSharedScene(GameSceneFlow.DepartureHubScene);
            return true;
        }

        public bool Join(string address, ushort port = DefaultPort)
        {
            if (!CanStart()) return false;
            if (string.IsNullOrWhiteSpace(address))
            {
                status = "서버 주소를 입력하세요";
                return false;
            }
            transport.SetConnectionData(address.Trim(), port);
            if (!manager.StartClient())
            {
                status = "서버 접속 실패";
                return false;
            }
            status = "서버에 접속 중...";
            return true;
        }

        public void RequestMission()
        {
            if (!IsConnected) return;
            if (manager.IsServer)
            {
                LoadSharedScene(GameSceneFlow.MissionPrototypeScene);
                return;
            }
            var avatar = manager.SpawnManager.GetLocalPlayerObject()?.GetComponent<NetworkPlayerAvatar>();
            avatar?.RequestMissionServerRpc();
        }

        public void RequestReturnToHub()
        {
            if (!IsConnected) return;
            if (manager.IsServer)
            {
                LoadSharedScene(GameSceneFlow.DepartureHubScene);
                return;
            }
            var avatar = manager.SpawnManager.GetLocalPlayerObject()?.GetComponent<NetworkPlayerAvatar>();
            avatar?.RequestHubServerRpc();
        }

        public void LoadSharedScene(string sceneName)
        {
            if (manager == null || !manager.IsServer) return;
            if (SceneManager.GetActiveScene().name == sceneName) return;
            manager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public void Disconnect()
        {
            disconnecting = true;
            if (manager != null && manager.IsListening) manager.Shutdown();
            status = "연결되지 않음";
            disconnecting = false;
        }

        private bool CanStart()
        {
            if (manager == null || manager.NetworkConfig.PlayerPrefab == null)
            {
                status = "네트워크 플레이어 에셋이 없습니다";
                Debug.LogError("NetworkPlayerAvatar prefab is missing from Resources/Network.");
                return false;
            }
            if (manager.IsListening)
            {
                status = "이미 연결되어 있습니다";
                return false;
            }
            return true;
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log("AD_NETWORK_CONNECTED " + clientId);
            if (manager != null && clientId == manager.LocalClientId)
                status = manager.IsServer ? "호스트 실행 중" : "서버에 연결됨";
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (manager == null || clientId != manager.LocalClientId) return;
            status = "서버 연결이 끊어졌습니다";
            if (!disconnecting && SceneManager.GetActiveScene().name != GameSceneFlow.FrontendScene)
                SceneManager.LoadScene(GameSceneFlow.FrontendScene);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsConnected) Debug.Log("AD_NETWORK_SCENE " + scene.name);
        }
    }
}
