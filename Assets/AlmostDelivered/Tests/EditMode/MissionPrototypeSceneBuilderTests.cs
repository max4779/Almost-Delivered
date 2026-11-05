using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Editor.Frontend;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionPrototypeSceneBuilderTests
    {
        [Test]
        public void BuildCreatesPlayableMissionWithPlayerBossesCameraAndHud()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);

            var player = Find(scene, "Player");
            var warden = Find(scene, "Warden Boss");
            var charger = Find(scene, "Charger Boss");
            var camera = Find(scene, "Main Camera");

            Assert.That(player.GetComponent<MissionPlayerController>(), Is.Not.Null);
            Assert.That(player.GetComponent<RangedAttackController>(), Is.Not.Null);
            Assert.That(player.GetComponent<CombatTeam>().Team, Is.EqualTo(CombatTeamId.Player));
            Assert.That(warden.GetComponent<PrototypeBossController>().Mode, Is.EqualTo(PrototypeBossMode.RangedWarden));
            Assert.That(charger.GetComponent<PrototypeBossController>().Mode, Is.EqualTo(PrototypeBossMode.IronCharger));
            Assert.That(Object.FindObjectsOfType<MissionHud>(true).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<CombatProjectile>(true).Count(projectile => projectile.name == "Projectile Template"), Is.EqualTo(1));

            var follow = camera.GetComponent<QuarterViewCamera>();
            Assert.That(camera.GetComponent<Camera>().orthographicSize, Is.EqualTo(9f));
            Assert.That(follow.Target, Is.EqualTo(player.transform));
            Assert.That(follow.Offset, Is.EqualTo(new Vector3(0f, 16f, -16f)));
            Assert.That(follow.FocusOffset, Is.EqualTo(new Vector3(0f, 0f, 3f)));

            var buildPaths = EditorBuildSettings.scenes.Select(item => item.path).ToArray();
            Assert.That(buildPaths.Take(3), Is.EqualTo(new[]
            {
                FrontendSceneBuilder.FrontendScenePath,
                FrontendSceneBuilder.DepartureHubScenePath,
                MissionPrototypeSceneBuilder.ScenePath
            }));
        }

        [Test]
        public void RepeatedBuildKeepsOneMissionRootAndOneBuildEntry()
        {
            FrontendSceneBuilder.BuildOrUpdateAll();
            FrontendSceneBuilder.BuildOrUpdateAll();
            var scene = EditorSceneManager.OpenScene(MissionPrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);

            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Mission Prototype"), Is.EqualTo(1));
            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Player"), Is.EqualTo(0));
            Assert.That(EditorBuildSettings.scenes.Count(item => item.path == MissionPrototypeSceneBuilder.ScenePath), Is.EqualTo(1));
        }

        private static GameObject Find(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Single(candidate => candidate.name == name);
        }
    }
}
