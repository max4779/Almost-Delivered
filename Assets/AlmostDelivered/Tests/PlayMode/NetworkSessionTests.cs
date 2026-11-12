using System.Collections;
using AlmostDelivered.Frontend;
using AlmostDelivered.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class NetworkSessionTests
    {
        [UnityTest]
        public IEnumerator HostLoadsSharedHubAndOwnsAnAvatar()
        {
            SceneManager.LoadScene(GameSceneFlow.FrontendScene);
            yield return null;

            var session = NetworkSession.Ensure();
            Assert.That(session.StartHost(27889), Is.True, session.Status);

            for (var i = 0; i < 180 && SceneManager.GetActiveScene().name != GameSceneFlow.DepartureHubScene; i++)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSceneFlow.DepartureHubScene));
            Assert.That(session.PlayerCount, Is.EqualTo(1));
            var avatar = Object.FindObjectOfType<NetworkPlayerAvatar>();
            Assert.That(avatar, Is.Not.Null);
            Assert.That(avatar.IsOwner, Is.True);

            session.LoadSharedScene(GameSceneFlow.MissionPrototypeScene);
            for (var i = 0; i < 180 && SceneManager.GetActiveScene().name != GameSceneFlow.MissionPrototypeScene; i++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSceneFlow.MissionPrototypeScene));
            Assert.That(Object.FindObjectOfType<NetworkPlayerAvatar>(), Is.Not.Null);

            session.Disconnect();
            Object.Destroy(session.gameObject);
            yield return null;
            var loadedScene = SceneManager.GetActiveScene();
            var emptyScene = SceneManager.CreateScene("Network Test Cleanup");
            SceneManager.SetActiveScene(emptyScene);
            yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
