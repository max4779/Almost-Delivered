using System;
using System.Collections;
using AlmostDelivered.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class ProceduralPoseAnimatorTests
    {
        private GameObject actor;
        private Transform visualRoot;
        private Transform[] pivots;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private Vector3[] scales;
        private ProceduralPoseAnimator animator;
        private Vector3 expectedRootPosition;
        private Quaternion expectedRootRotation;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Gameplay root");
            actor.transform.SetPositionAndRotation(new Vector3(3f, 2f, -4f), Quaternion.Euler(0f, 37f, 0f));
            visualRoot = Child(actor.transform, "VisualRoot", Vector3.zero, Vector3.zero);
            var body = Child(visualRoot, "Body", new Vector3(.1f, .9f, -.2f), new Vector3(3f, 8f, 5f));
            var left = Child(body, "Left arm", new Vector3(-.4f, .3f, .1f), new Vector3(7f, -4f, 11f));
            var right = Child(body, "Right arm", new Vector3(.4f, .3f, .1f), new Vector3(-5f, 6f, -9f));
            var leftLeg = Child(body, "Left leg", new Vector3(-.2f, -.35f, 0f), Vector3.zero);
            var rightLeg = Child(body, "Right leg", new Vector3(.2f, -.35f, 0f), Vector3.zero);
            pivots = new[] { body, left, right, leftLeg, rightLeg };
            positions = new Vector3[pivots.Length];
            rotations = new Quaternion[pivots.Length];
            scales = new Vector3[pivots.Length];
            for (var i = 0; i < pivots.Length; i++)
            {
                pivots[i].localScale = Vector3.one * (.8f + .1f * i);
                positions[i] = pivots[i].localPosition;
                rotations[i] = pivots[i].localRotation;
                scales[i] = pivots[i].localScale;
            }
            expectedRootPosition = actor.transform.position;
            expectedRootRotation = actor.transform.rotation;
            animator = visualRoot.gameObject.AddComponent<ProceduralPoseAnimator>();
            animator.Configure(body, left, right, false, leftLeg, rightLeg);
            animator.BindMotionSource(actor.transform);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(actor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Moving_AlternatesArmPivotsWithoutMovingGameplayRoot()
        {
            var sawPositive = false;
            var sawNegative = false;
            var sawOpposed = false;
            var sawWalkingLegs = false;
            var until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until)
            {
                MoveSource();
                yield return null;
                AssertRootUnchanged();
                var left = Mathf.DeltaAngle(0f, (Quaternion.Inverse(rotations[1]) * pivots[1].localRotation).eulerAngles.x);
                var right = Mathf.DeltaAngle(0f, (Quaternion.Inverse(rotations[2]) * pivots[2].localRotation).eulerAngles.x);
                sawPositive |= left > 5f;
                sawNegative |= left < -5f;
                sawOpposed |= left * right < -25f;
                var legLeft = Mathf.DeltaAngle(0f, (Quaternion.Inverse(rotations[3]) * pivots[3].localRotation).eulerAngles.x);
                var legRight = Mathf.DeltaAngle(0f, (Quaternion.Inverse(rotations[4]) * pivots[4].localRotation).eulerAngles.x);
                sawWalkingLegs |= legLeft * legRight < -40f;
            }
            Assert.That(sawPositive && sawNegative && sawOpposed, Is.True, "Arms must swing in opposite phases, in both directions.");
            Assert.That(sawWalkingLegs, Is.True, "Walking must swing both legs in opposite phases.");
        }

        [UnityTest]
        public IEnumerator Idle_ReturnsAllConfiguredPivotsToRecordedNeutralPose()
        {
            var until = Time.realtimeSinceStartup + .6f;
            var largestAngle = 0f;
            while (Time.realtimeSinceStartup < until)
            {
                MoveSource();
                yield return null;
                AssertRootUnchanged();
                largestAngle = Mathf.Max(largestAngle, Quaternion.Angle(rotations[1], pivots[1].localRotation));
            }
            Assert.That(largestAngle, Is.GreaterThan(5f));
            yield return new WaitForSecondsRealtime(1.5f);
            AssertNeutral();
            AssertRootUnchanged();
        }

        [UnityTest]
        public IEnumerator AttackAndHit_AreOneShotAndReturnToNeutralWithoutDrift()
        {
            for (var repeat = 0; repeat < 3; repeat++)
            {
                animator.PlayAttack();
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(Quaternion.Angle(rotations[2], pivots[2].localRotation), Is.GreaterThan(1f));
                animator.PlayHit();
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(Quaternion.Angle(rotations[0], pivots[0].localRotation), Is.GreaterThan(1f));
                yield return new WaitForSecondsRealtime(1.2f);
                AssertNeutral();
                AssertRootUnchanged();
            }
            yield return new WaitForSecondsRealtime(.4f);
            AssertNeutral();
        }

        [UnityTest]
        public IEnumerator MeleeAttack_SwingsArmAndBodyThenReturnsToNeutral()
        {
            animator.PlayMeleeAttack();
            yield return new WaitForSecondsRealtime(.17f);
            Assert.That(Quaternion.Angle(rotations[2], pivots[2].localRotation), Is.GreaterThan(25f));
            Assert.That(Quaternion.Angle(rotations[0], pivots[0].localRotation), Is.GreaterThan(3f));
            yield return new WaitForSecondsRealtime(1.3f);
            AssertNeutral();
        }

        [UnityTest]
        public IEnumerator PlayerSlash_SweepsLeftToRightAcrossHorizontalAxis()
        {
            animator.PlayPlayerSlash();
            yield return new WaitForSecondsRealtime(.04f);
            var first = Quaternion.Inverse(rotations[2]) * pivots[2].localRotation;
            var firstYaw = Mathf.DeltaAngle(0f, first.eulerAngles.y);
            Assert.That(firstYaw, Is.LessThan(-20f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, first.eulerAngles.x)), Is.LessThan(35f));

            yield return new WaitForSecondsRealtime(.1f);
            var second = Quaternion.Inverse(rotations[2]) * pivots[2].localRotation;
            var secondYaw = Mathf.DeltaAngle(0f, second.eulerAngles.y);
            Assert.That(secondYaw, Is.GreaterThan(15f));
            yield return new WaitForSecondsRealtime(1f);
            AssertNeutral();
        }

        [UnityTest]
        public IEnumerator Disable_RestoresNeutralPose()
        {
            animator.PlayAttack();
            animator.PlayHit();
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(Quaternion.Angle(rotations[0], pivots[0].localRotation), Is.GreaterThan(1f));
            animator.enabled = false;
            AssertNeutral(true);
            AssertRootUnchanged();
            MoveSource();
            animator.enabled = true;
            yield return null;
            yield return null;
            AssertNeutral();
            AssertRootUnchanged();
        }

        [Test]
        public void Configure_RejectsGameplayRootOrExternalPivots()
        {
            Assert.Throws<ArgumentException>(() => animator.Configure(actor.transform, pivots[1], pivots[2]));
            Assert.Throws<ArgumentException>(() => animator.Configure(visualRoot, pivots[1], pivots[2]));
            Assert.Throws<ArgumentException>(() => animator.BindMotionSource(pivots[0]));
            AssertNeutral(true);
        }

        [UnityTest]
        public IEnumerator VerticalMotion_DoesNotTriggerWalking()
        {
            for (var frame = 0; frame < 8; frame++)
            {
                expectedRootPosition += Vector3.up * .2f;
                actor.transform.position = expectedRootPosition;
                yield return null;
            }
            AssertNeutral();
            AssertRootUnchanged();
        }

        [UnityTest]
        public IEnumerator MissingArms_AnimatesBodyAndRestoresNeutral()
        {
            animator.Configure(pivots[0], null, null);
            animator.PlayHit();
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(Quaternion.Angle(rotations[0], pivots[0].localRotation), Is.GreaterThan(1f));
            yield return new WaitForSecondsRealtime(1.2f);
            AssertNeutral();
            AssertRootUnchanged();
        }

        private void MoveSource()
        {
            expectedRootPosition += new Vector3(.12f, 0f, .03f);
            actor.transform.position = expectedRootPosition;
        }

        private void AssertRootUnchanged()
        {
            Assert.That(actor.transform.position, Is.EqualTo(expectedRootPosition));
            Assert.That(actor.transform.rotation, Is.EqualTo(expectedRootRotation));
        }

        private void AssertNeutral(bool exact = false)
        {
            for (var i = 0; i < pivots.Length; i++)
            {
                Assert.That(Vector3.Distance(pivots[i].localPosition, positions[i]), Is.LessThanOrEqualTo(exact ? 0f : .001f));
                Assert.That(Quaternion.Angle(pivots[i].localRotation, rotations[i]), Is.LessThanOrEqualTo(.1f));
                Assert.That(Vector3.Distance(pivots[i].localScale, scales[i]), Is.LessThanOrEqualTo(exact ? 0f : .001f));

            }
        }

        private static Transform Child(Transform parent, string name, Vector3 position, Vector3 rotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localRotation = Quaternion.Euler(rotation);
            return child;
        }
    }
}
