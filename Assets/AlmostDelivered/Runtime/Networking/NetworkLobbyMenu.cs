using AlmostDelivered.Frontend;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Networking
{
    /// <summary>Temporary connection controls until the finished lobby UI is built.</summary>
    public sealed class NetworkLobbyMenu : MonoBehaviour
    {
        private string serverAddress = "127.0.0.1";
        private string portText = NetworkSession.DefaultPort.ToString();

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != GameSceneFlow.FrontendScene) return;
            var panel = new Rect(Screen.width - 300f, Screen.height - 250f, 280f, 230f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("MULTIPLAYER TEST");
            GUILayout.Label("Server address");
            serverAddress = GUILayout.TextField(serverAddress);
            GUILayout.Label("Port");
            portText = GUILayout.TextField(portText);
            var validPort = ushort.TryParse(portText, out var port) && port > 0;
            GUI.enabled = validPort;
            if (GUILayout.Button("Host game")) NetworkSession.Ensure().StartHost(port);
            if (GUILayout.Button("Join game")) NetworkSession.Ensure().Join(serverAddress, port);
            GUI.enabled = true;
            GUILayout.Label(NetworkSession.Instance?.Status ?? "Not connected");
            GUILayout.EndArea();
        }
    }
}
