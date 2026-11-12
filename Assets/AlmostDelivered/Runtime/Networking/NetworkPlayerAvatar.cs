using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Networking
{
    /// <summary>First multiplayer milestone: a visible, synchronized teammate in hub and mission.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour
    {
        [SerializeField, Min(.02f)] private float sendInterval = .1f;
        private Transform localActor;
        private Renderer[] renderers;
        private float nextSendTime;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public override void OnNetworkSpawn()
        {
            SetVisible(!IsOwner);
            Debug.Log("AD_NETWORK_AVATAR " + OwnerClientId + (IsOwner ? " OWNER" : " REMOTE"));
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindLocalActor();
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (localActor == null) FindLocalActor();
            if (localActor == null || Time.unscaledTime < nextSendTime) return;
            nextSendTime = Time.unscaledTime + sendInterval;
            if (IsServer)
            {
                ApplyPose(localActor.position, localActor.rotation);
            }
            else
            {
                SendPoseServerRpc(localActor.position, localActor.rotation);
            }
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendPoseServerRpc(Vector3 position, Quaternion rotation)
        {
            ApplyPose(position, rotation);
        }

        [ServerRpc]
        public void RequestMissionServerRpc()
        {
            if (SceneManager.GetActiveScene().name != GameSceneFlow.DepartureHubScene) return;
            // Gate proximity is checked again by the server before changing the shared scene.
            if ((transform.position - new Vector3(0f, transform.position.y, 8.2f)).sqrMagnitude > 36f) return;
            NetworkSession.Instance?.LoadSharedScene(GameSceneFlow.MissionPrototypeScene);
        }

        [ServerRpc]
        public void RequestHubServerRpc()
        {
            if (SceneManager.GetActiveScene().name == GameSceneFlow.MissionPrototypeScene)
                NetworkSession.Instance?.LoadSharedScene(GameSceneFlow.DepartureHubScene);
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            if (!Finite(position) || !Finite(rotation)) return;
            transform.SetPositionAndRotation(position, rotation);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            localActor = null;
        }

        private void FindLocalActor()
        {
            if (!IsOwner) return;
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == GameSceneFlow.DepartureHubScene)
            {
                var player = FindObjectOfType<HubPlayerController>();
                localActor = player != null ? player.transform : null;
            }
            else if (sceneName == GameSceneFlow.MissionPrototypeScene)
            {
                var player = FindObjectOfType<MissionPlayerController>();
                localActor = player != null ? player.transform : null;
            }
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null) return;
            foreach (var part in renderers) part.enabled = visible;
        }

        private static bool Finite(Vector3 value) =>
            Finite(value.x) && Finite(value.y) && Finite(value.z);

        private static bool Finite(Quaternion value) =>
            Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
