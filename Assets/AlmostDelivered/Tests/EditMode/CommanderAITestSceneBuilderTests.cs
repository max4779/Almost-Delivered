using System;
using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Presentation;
using AlmostDelivered.Presentation.Editor;
using AlmostDelivered.CommanderAI.Debugging;
using AlmostDelivered.CommanderAI.Execution;
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
            foreach (var signal in UnityEngine.Object.FindObjectsOfType<AlertSignal>())
            {
                var cue = signal.GetComponentInChildren<TextMesh>(true);
                Assert.That(cue, Is.Not.Null, "Every reporter has a saved alert cue.");
                Assert.That(cue.font, Is.Not.Null, "The saved cue requires a font for rendering.");
                Assert.That(cue.GetComponent<MeshRenderer>().sharedMaterial, Is.EqualTo(cue.font.material));
                Assert.That(cue.gameObject.activeSelf, Is.False);
            }
            foreach (var role in new[] { "Shield", "Ranged", "Charger", "Support" })
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>()).Count(t => t.name.StartsWith(role + " ")), Is.EqualTo(2));
            foreach (var pair in new[] { ("Player", VisualArchetype.Player), ("Dummy A", VisualArchetype.Dummy), ("Dummy B", VisualArchetype.Dummy), ("Commander", VisualArchetype.Commander), ("Shield 1", VisualArchetype.Shield), ("Shield 2", VisualArchetype.Shield), ("Ranged 1", VisualArchetype.Ranged), ("Ranged 2", VisualArchetype.Ranged), ("Charger 1", VisualArchetype.Charger), ("Charger 2", VisualArchetype.Charger), ("Support 1", VisualArchetype.Support), ("Support 2", VisualArchetype.Support) })
            {
                var actor = GameObject.Find(pair.Item1);
                var roots = actor.transform.Cast<Transform>().Where(t => t.name == "VisualRoot").ToArray();
                Assert.That(roots.Length, Is.EqualTo(1), pair.Item1);
                var visual = roots[0].GetComponent<LowPolyVisual>();
                Assert.That(visual.Archetype, Is.EqualTo(pair.Item2));
                Assert.That(visual.IsGameplayFree(out var reason), Is.True, reason);
                Assert.That(actor.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(actor.GetComponent<ActorHealth>(), Is.Not.Null);
                if (pair.Item1.EndsWith(" 1") || pair.Item1.EndsWith(" 2")) {
                    Assert.That(actor.GetComponent<NavMeshAgent>(), Is.Not.Null);
                    Assert.That(actor.GetComponent<MonsterOrderExecutor>(), Is.Not.Null);
                    Assert.That(actor.GetComponent<MonsterPerception>(), Is.Not.Null);
                    Assert.That(actor.GetComponent<Collider>(), Is.Not.Null);
                }
            }
            var objective = GameObject.Find("Fixed Objective");
            Assert.That(objective.transform.Find("VisualRoot").GetComponent<LowPolyVisual>().Archetype, Is.EqualTo(VisualArchetype.ObjectiveBeacon));
            var beacon = objective.transform.Find("VisualRoot").GetComponent<LowPolyVisual>();
            var payload = objective.transform.Find("Payload display/VisualRoot").GetComponent<LowPolyVisual>();
            Assert.That(payload.Archetype, Is.EqualTo(VisualArchetype.Payload));
            Assert.That(beacon.IsGameplayFree(out var beaconReason), Is.True, beaconReason);
            Assert.That(payload.IsGameplayFree(out var payloadReason), Is.True, payloadReason);
            Assert.That(Vector3.Distance(UnityEngine.Object.FindObjectOfType<CommanderArena>().Objective.position, new Vector3(0, 1, 0)), Is.LessThan(.0001f));
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
            var paths = Enum.GetValues(typeof(VisualArchetype)).Cast<VisualArchetype>().Select(LowPolyAssetFactory.GetPrefabPath)
                .Concat(AssetDatabase.FindAssets("t:Material", new[] { "Assets/AlmostDelivered/Art/Materials/Generated" }).Select(AssetDatabase.GUIDToAssetPath)).ToArray();
            var visualGuids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Assert.That(visualGuids.All(value => !string.IsNullOrEmpty(value)), Is.True);
            var count = UnityEngine.Object.FindObjectsOfType<LowPolyVisual>().Length;
            Build();
            Assert.That(paths.Select(AssetDatabase.AssetPathToGUID), Is.EqualTo(visualGuids));
            Assert.That(UnityEngine.Object.FindObjectsOfType<LowPolyVisual>().Length, Is.EqualTo(count));
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/AlmostDelivered/Configs/CommanderAI/Normal.asset"), Is.EqualTo(guid));
            Assert.That(NavMesh.CalculateTriangulation().vertices.Length, Is.GreaterThan(0), "Rebuilding must load the persisted navigation data.");
            Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.EqualTo(sceneGuid));
            Assert.That(UnityEngine.Object.FindObjectsOfType<ObservableActor>().Length, Is.EqualTo(3));
            Assert.That(UnityEngine.Object.FindObjectsOfType<MonsterPerception>().Length, Is.EqualTo(8));
        }
    }
}
