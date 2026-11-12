using UnityEngine;
using AlmostDelivered.Networking;

namespace AlmostDelivered.Frontend
{
    public sealed class FrontendMenuController : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<NetworkLobbyMenu>() == null)
                gameObject.AddComponent<NetworkLobbyMenu>();
        }

        public void StartGame()
        {
            GameSceneFlow.LoadDepartureHub();
        }

        public void OpenAccount()
        {
            GetComponent<FrontendAccountPanel>()?.OpenLogin();
        }

        public void QuitGame()
        {
            GameSceneFlow.QuitGame();
        }
    }
}
