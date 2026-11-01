using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class TacticalSlotAllocatorTests
    {
        private GameObject root;
        private readonly List<TacticalAnchor> anchors = new List<TacticalAnchor>();
        private TacticalSlotAllocator allocator;
        private Reachability reachability;
        private TacticalSlotRequest request;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Tactical test");
            anchors.Clear();
            reachability = new Reachability();
            allocator = new TacticalSlotAllocator(anchors, reachability);
            request = new TacticalSlotRequest(Vector3.zero, TacticalAnchorTag.ObjectiveFront);
            Member(1); Member(2); Member(3);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test] public void RoleFiltering_RejectsIncompatibleAnchor()
        {
            Anchor(Vector3.zero, MonsterRole.Ranged);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out _), Is.False);
        }

        [Test] public void TagFiltering_RejectsDifferentTag()
        {
            Anchor(Vector3.zero).Tag = TacticalAnchorTag.CoverRear;
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out _), Is.False);
        }

        [Test] public void Capacity_AllowsOnlyConfiguredNumber()
        {
            Anchor(Vector3.zero).Capacity = 2;
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out _), Is.True);
            Assert.That(allocator.TryAllocate(2, MonsterRole.Shield, in request, out _), Is.True);
            Assert.That(allocator.TryAllocate(3, MonsterRole.Shield, in request, out _), Is.False);
        }

        [Test] public void NearestScore_SelectsNearestReachableAnchor()
        {
            Anchor(Vector3.right * 5);
            var nearest = Anchor(Vector3.right);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(nearest));
        }

        [Test] public void EqualScore_UsesAscendingInstanceIdRegardlessOfInputOrder()
        {
            var a = Anchor(Vector3.right);
            var b = Anchor(Vector3.left);
            var expected = a.GetInstanceID() < b.GetInstanceID() ? a : b;
            anchors.Sort((left, right) => right.GetInstanceID().CompareTo(left.GetInstanceID()));
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(expected));
        }

        [Test] public void RepeatedAllocation_DoesNotReserveTwice()
        {
            Anchor(Vector3.zero).Capacity = 2;
            allocator.TryAllocate(1, MonsterRole.Shield, in request, out var first);
            allocator.TryAllocate(1, MonsterRole.Shield, in request, out var second);
            Assert.That(second, Is.SameAs(first));
            Assert.That(allocator.TryAllocate(2, MonsterRole.Shield, in request, out _), Is.True);
        }

        [Test] public void DestroyedAnchor_IsRemovedAndMemberCanAllocateAgain()
        {
            var old = Anchor(Vector3.zero);
            allocator.TryAllocate(1, MonsterRole.Shield, in request, out _);
            Object.DestroyImmediate(old.gameObject);
            var replacement = Anchor(Vector3.right);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(replacement));
            Assert.That(allocator.ReservationCount, Is.EqualTo(1));
        }

        [Test] public void PartialAllocation_PreservesSuccessfulReservations()
        {
            var only = Anchor(Vector3.zero);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out _), Is.True);
            Assert.That(allocator.TryAllocate(2, MonsterRole.Shield, in request, out _), Is.False);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(only));
        }

        [Test] public void ReleaseByMember_FreesOnlyThatReservation()
        {
            Anchor(Vector3.zero).Capacity = 2;
            allocator.TryAllocate(1, MonsterRole.Shield, in request, out _);
            allocator.TryAllocate(2, MonsterRole.Shield, in request, out _);
            allocator.Release(1);
            Assert.That(allocator.ReservationCount, Is.EqualTo(1));
            Assert.That(allocator.TryAllocate(3, MonsterRole.Shield, in request, out _), Is.True);
            allocator.ReleaseAll();
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        [Test] public void UnreachableAnchor_IsRejectedUsingMemberPosition()
        {
            Anchor(Vector3.forward);
            reachability.Reachable = false;
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out _), Is.False);
            Assert.That(reachability.From, Is.EqualTo(Vector3.right * 7));
            Assert.That(reachability.To, Is.EqualTo(Vector3.forward));
        }

        [Test] public void DeadOrDestroyedMember_ReleasesCapacity()
        {
            Anchor(Vector3.zero);
            allocator.TryAllocate(1, MonsterRole.Shield, in request, out _);
            root.transform.Find("Member 1").GetComponent<ActorHealth>().ApplyDamage(100);
            Assert.That(allocator.TryAllocate(2, MonsterRole.Shield, in request, out _), Is.True);
            Object.DestroyImmediate(root.transform.Find("Member 2").gameObject);
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        [Test] public void UnregisteredDeadOrMismatchedRole_CannotAllocate()
        {
            Anchor(Vector3.zero);
            Assert.That(allocator.TryAllocate(99, MonsterRole.Shield, in request, out _), Is.False);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Ranged, in request, out _), Is.False);
            root.transform.Find("Member 1").GetComponent<ActorHealth>().ApplyDamage(100);
            Assert.That(allocator.TryAllocate(1, MonsterRole.Shield, in request, out _), Is.False);
        }

        [Test] public void DisabledAnchor_ReleasesReservation()
        {
            var anchor = Anchor(Vector3.zero);
            allocator.TryAllocate(1, MonsterRole.Shield, in request, out _);
            anchor.gameObject.SetActive(false);
            Assert.That(allocator.ReservationCount, Is.Zero);
        }

        private void Member(int id)
        {
            var member = new GameObject("Member " + id);
            member.transform.SetParent(root.transform);
            member.transform.position = Vector3.right * 7;
            allocator.RegisterMember(id, MonsterRole.Shield, member.AddComponent<ActorHealth>());
        }

        private TacticalAnchor Anchor(Vector3 position, MonsterRole role = MonsterRole.Shield)
        {
            var item = new GameObject("Anchor");
            item.transform.SetParent(root.transform);
            item.transform.position = position;
            var anchor = item.AddComponent<TacticalAnchor>();
            anchor.Tag = TacticalAnchorTag.ObjectiveFront;
            anchor.AllowedRoles = new[] { role };
            anchors.Add(anchor);
            return anchor;
        }

        private sealed class Reachability : ITacticalReachability
        {
            public bool Reachable = true;
            public Vector3 From, To;
            public bool CanReach(Vector3 from, Vector3 to) { From = from; To = to; return Reachable; }
        }
    }
}
