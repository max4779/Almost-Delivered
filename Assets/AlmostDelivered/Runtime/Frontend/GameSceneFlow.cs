using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using AlmostDelivered.Networking;

namespace AlmostDelivered.Frontend
{
    public static class GameSceneFlow
    {
        public const string FrontendScene = "Frontend";
        public const string DepartureHubScene = "DepartureHub";
        public const string MissionPrototypeScene = "MissionPrototype";

        public static bool IsKnownScene(string sceneName)
        {
            return string.Equals(sceneName, FrontendScene, StringComparison.Ordinal)
                || string.Equals(sceneName, DepartureHubScene, StringComparison.Ordinal)
                || string.Equals(sceneName, MissionPrototypeScene, StringComparison.Ordinal);
        }

        public static void LoadScene(string sceneName)
        {
            if (!IsKnownScene(sceneName))
            {
                throw new ArgumentException("Scene must be a declared frontend scene.", nameof(sceneName));
            }

            if (NetworkSession.IsConnected)
            {
                if (sceneName == FrontendScene)
                    NetworkSession.Instance.Disconnect();
                else if (sceneName == MissionPrototypeScene && NetworkSession.IsClientOnly)
                {
                    NetworkSession.Instance.RequestMission();
                    return;
                }
                else if (sceneName == DepartureHubScene && NetworkSession.IsClientOnly)
                {
                    NetworkSession.Instance.RequestReturnToHub();
                    return;
                }
                else if (NetworkSession.IsClientOnly)
                    return;
                else
                {
                    NetworkSession.Instance.LoadSharedScene(sceneName);
                    return;
                }
            }
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public static void LoadDepartureHub()
        {
            LoadScene(DepartureHubScene);
        }

        public static void LoadFrontend()
        {
            LoadScene(FrontendScene);
        }

        public static void LoadMissionPrototype()
        {
            LoadScene(MissionPrototypeScene);
        }

        public static void QuitGame()
        {
            Application.Quit();
        }
    }
}
