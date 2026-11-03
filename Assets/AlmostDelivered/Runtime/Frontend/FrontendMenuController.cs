using UnityEngine;

namespace AlmostDelivered.Frontend
{
    public sealed class FrontendMenuController : MonoBehaviour
    {
        public void StartGame()
        {
            GameSceneFlow.LoadDepartureHub();
        }

        public void QuitGame()
        {
            GameSceneFlow.QuitGame();
        }
    }
}
