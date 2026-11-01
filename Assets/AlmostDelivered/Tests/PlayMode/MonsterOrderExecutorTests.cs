using System.Collections;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class MonsterOrderExecutorTests
    {
        private GameObject root;
        private MonsterOrderExecutor executor;
        private TacticalSlotAllocator allocator;
        private TacticalAnchor anchor;
        private SquadKnowledge knowledge;
        private NavMeshData data;
        private NavMeshDataInstance mesh;
        private NavMeshAgent agent;

        [SetUp] public void SetUp()
        {
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),
                new List<NavMeshBuildSource> { new NavMeshBuildSource {
                    shape = NavMeshBuildSourceShape.Box, size = new Vector3(30, .2f, 30),
                    transform = Matrix4x4.TRS(Vector3.down * .1f, Quaternion.identity, Vector3.one), area = 0 } },
                new Bounds(Vector3.zero, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            mesh = NavMesh.AddNavMeshData(data);
            root = new GameObject("Orders test");
            var member = Child("Member", Vector3.zero);
            var health = member.AddComponent<ActorHealth>();
            agent = member.AddComponent<NavMeshAgent>();
            agent.speed = 12; agent.acceleration = 100; agent.angularSpeed = 720; agent.stoppingDistance = .15f;
            Assert.That(agent.Warp(Vector3.zero), Is.True);
            executor = member.AddComponent<MonsterOrderExecutor>();
            anchor = Child("Slot", Vector3.right * 2).AddComponent<TacticalAnchor>();
            allocator = new TacticalSlotAllocator(new[] { anchor }, new NavMeshTacticalReachability());
            allocator.RegisterMember(17, MonsterRole.Shield, health);
            knowledge = new SquadKnowledge();
            executor.Configure(17, allocator, knowledge);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
            mesh.Remove();
            Object.Destroy(data);
        }

        [UnityTest] public IEnumerator MoveToSlot_UsesAgentAndThenHoldsReservation()
        {
            Reserve();
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            yield return WaitForHold();
            Assert.That(Vector3.Distance(executor.transform.position, anchor.transform.position), Is.LessThan(.4f));
            Assert.That(allocator.ReservationCount, Is.EqualTo(1));
            Assert.That(agent.hasPath, Is.False);
        }

        [UnityTest] public IEnumerator HoldSlot_ReturnsToItsAnchorAfterDisplacement()
        {
            executor.Assign(new MonsterOrder(MonsterOrderType.HoldSlot, anchor: anchor));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(agent.Warp(Vector3.left * 2), Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Vector3.Distance(executor.transform.position, anchor.transform.position), Is.LessThan(.4f));
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }

        [UnityTest] public IEnumerator AttackObservedTarget_DamagesOnlyMemoryIdOnCooldown()
        {
            var seen = Target("Observed", Vector3.forward * .5f);
            var other = Target("Unobserved closer", Vector3.forward * .2f);
            Observe(seen, seen.transform.position);
            var attacks = 0;
            executor.LocalAttack += id => { Assert.That(id, Is.EqualTo(seen.ActorId)); attacks++; };
            executor.Assign(new MonsterOrder(MonsterOrderType.AttackObservedTarget, targetActorId: seen.ActorId));
            yield return null;
            Assert.That(seen.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(90));
            Assert.That(other.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(attacks, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(attacks, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator AttackObservedTarget_WithoutMemory_DoesNotAttack()
        {
            var target = Target("Unknown", Vector3.forward * .5f);
            executor.Assign(new MonsterOrder(MonsterOrderType.AttackObservedTarget, targetActorId: target.ActorId));
            yield return null;
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100));
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }

        [UnityTest] public IEnumerator AttackObservedTarget_MovesToMemoryInsteadOfHiddenLivePosition()
        {
            var target = Target("Hidden moved target", Vector3.left * 8);
            Observe(target, Vector3.right * 3);
            executor.Assign(new MonsterOrder(MonsterOrderType.AttackObservedTarget, targetActorId: target.ActorId));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(executor.transform.position.x, Is.GreaterThan(2));
            Assert.That(target.GetComponent<ActorHealth>().CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest] public IEnumerator ProtectUnit_FollowsTheAssignedFriendly()
        {
            var friendly = Child("Friendly", Vector3.forward * 2);
            executor.Assign(new MonsterOrder(MonsterOrderType.ProtectUnit, protectedUnit: friendly.transform));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Vector3.Distance(executor.transform.position, friendly.transform.position), Is.LessThan(.4f));
            friendly.transform.position = Vector3.forward * 4;
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Vector3.Distance(executor.transform.position, friendly.transform.position), Is.LessThan(.4f));
        }

        [UnityTest] public IEnumerator SearchLastKnownPosition_MovesAndHoldsAtSuppliedPosition()
        {
            executor.Assign(new MonsterOrder(MonsterOrderType.SearchLastKnownPosition, position: Vector3.forward * 3));
            yield return WaitForHold();
            Assert.That(Vector3.Distance(executor.transform.position, Vector3.forward * 3), Is.LessThan(.4f));
        }

        [UnityTest] public IEnumerator InvalidAnchor_RequestsExactlyOnceThenFallsBack()
        {
            Reserve();
            var requests = 0;
            executor.ReassignmentRequested += id => { Assert.That(id, Is.EqualTo(17)); requests++; };
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            Object.Destroy(anchor.gameObject);
            yield return null; yield return null; yield return null;
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        [UnityTest] public IEnumerator InvalidReassignment_DoesNotRestartRetryBudget()
        {
            var requests = 0;
            executor.ReassignmentRequested += _ => {
                requests++;
                executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot));
            };
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot));
            yield return null; yield return null; yield return null;
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }

        [UnityTest] public IEnumerator ValidReassignment_ReachesReplacementSlot()
        {
            var replacement = Child("Replacement", Vector3.left * 2).AddComponent<TacticalAnchor>();
            var requests = 0;
            executor.ReassignmentRequested += _ => {
                requests++;
                executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: replacement));
            };
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot));
            yield return WaitForHold();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(Vector3.Distance(executor.transform.position, replacement.transform.position), Is.LessThan(.4f));
        }

        [UnityTest] public IEnumerator UnreachableSlot_RequestsOnceThenFallsBack()
        {
            anchor.transform.position = Vector3.right * 100;
            var requests = 0;
            executor.ReassignmentRequested += _ => requests++;
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }

        [UnityTest] public IEnumerator CancelAndDestruction_ReleaseReservations()
        {
            Reserve();
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            yield return null;
            executor.Cancel();
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.None));
            Assert.That(allocator.ReservationCount, Is.Zero);
            Reserve();
            Object.Destroy(executor.gameObject);
            yield return null;
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        [UnityTest] public IEnumerator Death_CancelsAndReleasesReservation()
        {
            Reserve();
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            executor.GetComponent<ActorHealth>().ApplyDamage(100);
            yield return null;
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.None));
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        [UnityTest] public IEnumerator AgentUnavailable_FallbackDoesNotRequestAgain()
        {
            agent.enabled = false;
            var requests = 0;
            executor.ReassignmentRequested += _ => requests++;
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            for (var frame = 0; frame < 8; frame++) yield return null;
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }

        [UnityTest] public IEnumerator AssignPositionalHold_ReleasesPreviousAnchor()
        {
            Reserve();
            executor.Assign(new MonsterOrder(MonsterOrderType.MoveToSlot, anchor: anchor));
            executor.Assign(new MonsterOrder(MonsterOrderType.HoldSlot, position: Vector3.zero));
            yield return null;
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        [UnityTest] public IEnumerator DestroyedHeldAnchor_RequestsOnceThenReleases()
        {
            Reserve();
            executor.Assign(new MonsterOrder(MonsterOrderType.HoldSlot, anchor: anchor));
            var requests = 0;
            executor.ReassignmentRequested += _ => requests++;
            Object.Destroy(anchor.gameObject);
            yield return null; yield return null; yield return null;
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(allocator.ReservationCount, Is.Zero);
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }

        private void Reserve()
        {
            var request = new TacticalSlotRequest(Vector3.zero, TacticalAnchorTag.ObjectiveFront);
            Assert.That(allocator.TryAllocate(17, MonsterRole.Shield, in request, out _), Is.True);
        }
        private GameObject Child(string name, Vector3 position)
        {
            var child = new GameObject(name); child.transform.SetParent(root.transform); child.transform.position = position; return child;
        }
        private ObservableActor Target(string name, Vector3 position)
        {
            var target = Child(name, position); target.AddComponent<ActorHealth>(); target.AddComponent<SphereCollider>();
            Physics.SyncTransforms(); return target.AddComponent<ObservableActor>();
        }
        private void Observe(ObservableActor actor, Vector3 position) => knowledge.Merge(new ObservationSample(actor.ActorId, position,
            Vector3.zero, Time.unscaledTime, 1, ObservedCombatTag.None, 17));
        private IEnumerator WaitForHold()
        {
            var deadline = Time.realtimeSinceStartup + 3;
            do { yield return null; } while (executor.CurrentOrder.Type != MonsterOrderType.HoldSlot && Time.realtimeSinceStartup < deadline);
            Assert.That(executor.CurrentOrder.Type, Is.EqualTo(MonsterOrderType.HoldSlot));
        }
    }
}
