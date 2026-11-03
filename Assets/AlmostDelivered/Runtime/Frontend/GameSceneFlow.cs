using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Frontend
{
    public static class GameSceneFlow
    {
        public const string FrontendScene = "Frontend";
        public const string DepartureHubScene = "DepartureHub";

        public static bool IsKnownScene(string sceneName)
        {
            return string.Equals(sceneName, FrontendScene, StringComparison.Ordinal)
                || string.Equals(sceneName, DepartureHubScene, StringComparison.Ordinal);
        }

        public static void LoadScene(string sceneName)
        {
            if (!IsKnownScene(sceneName))
            {
                throw new ArgumentException("Scene must be a declared frontend scene.", nameof(sceneName));
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

        public static void QuitGame()
        {
            Application.Quit();
        }
    }
}
