using System;
using System.Collections;
using AlmostDelivered.Frontend;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Networking
{
    /// <summary>Headless connection entry point for local two-process checks and future deployment.</summary>
    public static class NetworkCommandLineStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromArguments()
        {
            var args = Environment.GetCommandLineArgs();
            var host = Array.Exists(args, arg => arg == "--ad-host");
            var connect = Array.Find(args, arg => arg.StartsWith("--ad-connect=", StringComparison.Ordinal));
            if (!host && connect == null) return;

            Application.runInBackground = true;
            var portText = Array.Find(args, arg => arg.StartsWith("--ad-port=", StringComparison.Ordinal));
            var port = NetworkSession.DefaultPort;
            if (portText != null && ushort.TryParse(portText.Substring("--ad-port=".Length), out var parsed) && parsed > 0)
                port = parsed;

            var session = NetworkSession.Ensure();
            var started = host ? session.StartHost(port) : session.Join(connect.Substring("--ad-connect=".Length), port);
            Debug.Log(started ? "AD_NETWORK_STARTED " + (host ? "HOST" : "CLIENT") :
                "AD_NETWORK_START_FAILED " + session.Status);
            if (started && host && Array.Exists(args, arg => arg == "--ad-smoke-mission"))
                session.StartCoroutine(LoadMissionWhenPartnerArrives(session));
        }

        private static IEnumerator LoadMissionWhenPartnerArrives(NetworkSession session)
        {
            while (session != null && session.PlayerCount < 2) yield return null;
            if (session == null) yield break;
            yield return new WaitForSeconds(5f);
            if (session != null && SceneManager.GetActiveScene().name == GameSceneFlow.DepartureHubScene)
                session.LoadSharedScene(GameSceneFlow.MissionPrototypeScene);
        }
    }
}
