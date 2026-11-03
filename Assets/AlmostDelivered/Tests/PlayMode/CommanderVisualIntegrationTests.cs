using System.Collections;
using System.Linq;
using AlmostDelivered.CommanderAI.Debugging;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Perception;
using AlmostDelivered.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class CommanderVisualIntegrationTests
    {
        private CommanderArena arena;
        [UnitySetUp] public IEnumerator LoadArena()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/AlmostDelivered/Scenes/CommanderAITest.unity", new LoadSceneParameters(LoadSceneMode.Single));
            arena = Object.FindObjectOfType<CommanderArena>();
            yield return null;
            arena.Brain.enabled = false;
            foreach (var member in arena.Members) member.GetComponent<MonsterPerception>().enabled = false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var scene = arena.gameObject.scene;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Visual cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator ArenaStart_AllVisualRootsRemainGameplayFree()
        {
            var visuals = Object.FindObjectsOfType<LowPolyVisual>();
            Assert.That(visuals.Length, Is.EqualTo(14));
            foreach (var visual in visuals) Assert.That(visual.IsGameplayFree(out var reason), Is.True, reason);
            Assert.That(arena.Squad.State, Is.EqualTo(SquadAlertState.Unaware));
            yield return null;
        }
        [UnityTest] public IEnumerator MemberMovement_AnimatesVisualWithoutChangingNavMeshRootContract()
        {
            var member = arena.Members[0]; var agent = member.GetComponent<NavMeshAgent>();
            var visual = member.GetComponentInChildren<LowPolyVisual>(); var arm = visual.PosePivots[1];
            var rotation = arm.localRotation; var start = member.transform.position;
            Assert.That(agent.SetDestination(start + Vector3.right * 5), Is.True);
            var maxAngle = 0f; var deadline = Time.time + .5f;
            while (Time.time < deadline) {
                yield return null;
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rotation, arm.localRotation));
            }
            Assert.That(Vector3.Distance(start, member.transform.position), Is.GreaterThan(.1f));
            Assert.That(maxAngle, Is.GreaterThan(.1f));
            Assert.That(visual.transform.parent, Is.SameAs(member.transform));
            Assert.That(agent.enabled, Is.True);
        }
        [UnityTest] public IEnumerator CommanderDeath_VisualsRemainAttachedWhileOrdersBecomeConfused()
        {
            var visual = arena.Commander.GetComponentInChildren<LowPolyVisual>();
            arena.Commander.ApplyDamage(1000);
            yield return null;
            Assert.That(visual.transform.parent, Is.SameAs(arena.Commander.transform));
            Assert.That(arena.Squad.State, Is.EqualTo(SquadAlertState.Confused));
            Assert.That(arena.Members.All(member => member.CurrentOrder.Type == MonsterOrderType.ConfusedCombat), Is.True);
        }
        [UnityTest] public IEnumerator RepeatedMembers_ShareMaterialInstancesByIdentity()
        {
            var first = arena.Members[0].GetComponentInChildren<LowPolyVisual>();
            var second = arena.Members[1].GetComponentInChildren<LowPolyVisual>();
            var materials = first.Renderers.Select(r => r.sharedMaterial).ToArray();
            var count = Resources.FindObjectsOfTypeAll<Material>().Length;
            for (var i = 0; i < 10; i++) yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(count));
            for (var i = 0; i < materials.Length; i++) {
                Assert.That(first.Renderers[i].sharedMaterial, Is.SameAs(materials[i]));
                Assert.That(second.Renderers[i].sharedMaterial, Is.SameAs(materials[i]));
            }
        }
    }
}
