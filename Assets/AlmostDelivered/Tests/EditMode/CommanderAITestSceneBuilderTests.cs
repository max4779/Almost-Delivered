using System;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Perception;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class CommanderAITestSceneBuilderTests
    {
        private const string ScenePath = "Assets/AlmostDelivered/Scenes/CommanderAITest.unity";
        private static Scene Build()
        {
            var builder = Type.GetType("AlmostDelivered.Editor.CommanderAI.CommanderAITestSceneBuilder, AlmostDelivered.CommanderAI.Editor");
            Assert.That(builder, Is.Not.Null, "The deterministic arena builder must exist.");
            return (Scene)builder.GetMethod("BuildOrUpdateTestScene").Invoke(null, new object[] { ScenePath });
        }

        [TearDown] public void Cleanup() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test] public void Build_CreatesRequiredActorsAnchorsAndConfigs()
        {
            var scene = Build();
            Assert.That(scene.path, Is.EqualTo(ScenePath));
            Assert.That(UnityEngine.Object.FindObjectsOfType<ObservableActor>().Length, Is.EqualTo(3));
            Assert.That(GameObject.Find("Player").GetComponent<CharacterController>(), Is.Not.Null);
            foreach (var name in new[] { "Dummy A", "Dummy B" })
                Assert.That(GameObject.Find(name).GetComponent<ObservableActor>(), Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsOfType<SquadController>().Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsOfType<CommanderBrain>().Length, Is.EqualTo(1));
            Assert.That(GameObject.Find("Commander").GetComponent<ActorHealth>(), Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsOfType<MonsterPerception>().Length, Is.EqualTo(8));
            foreach (var role in new[] { "Shield", "Ranged", "Charger", "Support" })
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>()).Count(t => t.name.StartsWith(role + " ")), Is.EqualTo(2));
            Assert.That(GameObject.Find("Fixed Objective"), Is.Not.Null);
            var anchors = UnityEngine.Object.FindObjectsOfType<TacticalAnchor>();
            foreach (TacticalAnchorTag tag in Enum.GetValues(typeof(TacticalAnchorTag)))
                Assert.That(anchors.Any(anchor => anchor.Tag == tag), Is.True, tag.ToString());
            Assert.That(NavMesh.CalculateTriangulation().vertices.Length, Is.GreaterThan(0), "Navigation must be baked.");
            Assert.That(GameObject.Find("Navigation Ground").isStatic, Is.True);
            foreach (var difficulty in new[] { "Easy", "Normal", "Hard" })
                Assert.That(AssetDatabase.LoadAssetAtPath<CommanderDifficultyProfile>("Assets/AlmostDelivered/Configs/CommanderAI/" + difficulty + ".asset"), Is.Not.Null);
        }

        [Test] public void Build_RepeatedRunsPreserveAssetGuidsAndActorCounts()
        {
            Build();
            var guid = AssetDatabase.AssetPathToGUID("Assets/AlmostDelivered/Configs/CommanderAI/Normal.asset");
            var sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            Build();
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/AlmostDelivered/Configs/CommanderAI/Normal.asset"), Is.EqualTo(guid));
            Assert.That(NavMesh.CalculateTriangulation().vertices.Length, Is.GreaterThan(0), "Rebuilding must load the persisted navigation data.");
            Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.EqualTo(sceneGuid));
            Assert.That(UnityEngine.Object.FindObjectsOfType<ObservableActor>().Length, Is.EqualTo(3));
            Assert.That(UnityEngine.Object.FindObjectsOfType<MonsterPerception>().Length, Is.EqualTo(8));
        }
    }
}
