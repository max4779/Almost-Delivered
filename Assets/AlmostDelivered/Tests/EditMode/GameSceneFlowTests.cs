using System;
using System.Reflection;
using AlmostDelivered.Frontend;
using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class GameSceneFlowTests
    {
        [Test]
        public void SceneNamesAndValidationAreStable()
        {
            Assert.That(GameSceneFlow.FrontendScene, Is.EqualTo("Frontend"));
            Assert.That(GameSceneFlow.DepartureHubScene, Is.EqualTo("DepartureHub"));
            var missionScene = typeof(GameSceneFlow).GetField(
                "MissionPrototypeScene",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(missionScene, Is.Not.Null);
            Assert.That(missionScene.GetValue(null), Is.EqualTo("MissionPrototype"));
            Assert.That(GameSceneFlow.IsKnownScene("Frontend"), Is.True);
            Assert.That(GameSceneFlow.IsKnownScene("DepartureHub"), Is.True);
            Assert.That(GameSceneFlow.IsKnownScene("MissionPrototype"), Is.True);
            Assert.That(GameSceneFlow.IsKnownScene(""), Is.False);
            Assert.That(GameSceneFlow.IsKnownScene("CommanderAITest"), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Unknown")]
        public void LoadSceneRejectsUnknownNamesBeforeTouchingUnity(string sceneName)
        {
            Assert.That(
                () => GameSceneFlow.LoadScene(sceneName),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
