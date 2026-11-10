using AlmostDelivered.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class HubPlayerControllerTests
    {
        private GameObject player;
        private CharacterController characterController;
        private HubPlayerController movement;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Hub player test");
            characterController = player.AddComponent<CharacterController>();
            movement = player.AddComponent<HubPlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
        }

        [Test]
        public void MoveUsesNormalizedPlanarInputAndConfiguredSpeed()
        {
            movement.Speed = 6f;

            movement.Move(new Vector2(1f, 1f), .5f);

            var expected = new Vector3(1f, 0f, 1f).normalized * 3f;
            Assert.That(player.transform.position.x, Is.EqualTo(expected.x).Within(.001f));
            Assert.That(player.transform.position.z, Is.EqualTo(expected.z).Within(.001f));
        }

        [Test]
        public void MoveRequiresEnabledControllerAndFiniteInput()
        {
            characterController.enabled = false;
            movement.Move(Vector2.right, 1f);
            Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));

            characterController.enabled = true;
            movement.Move(new Vector2(float.NaN, 0f), 1f);
            movement.Move(Vector2.right, float.PositiveInfinity);
            Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MovingDoesNotOverrideMouseAimDirection()
        {
            Assert.That(movement.AimAt(Vector3.back * 5f, 1f), Is.True);
            movement.Move(Vector2.right, .1f);

            Assert.That(Vector3.Dot(player.transform.forward, Vector3.back), Is.GreaterThan(.999f));
        }

        [Test]
        public void CameraSnapMaintainsConfiguredQuarterViewOffset()
        {
            var cameraObject = new GameObject("Hub camera test");
            try
            {
                player.transform.position = new Vector3(3f, 0f, -2f);
                var follow = cameraObject.AddComponent<QuarterViewCamera>();
                follow.Target = player.transform;
                follow.Offset = new Vector3(0f, 12f, -9f);

                follow.SnapToTarget();

                Assert.That(cameraObject.transform.position, Is.EqualTo(new Vector3(3f, 12f, -11f)));
                var forwardToPlayer = (player.transform.position - cameraObject.transform.position).normalized;
                Assert.That(Vector3.Dot(cameraObject.transform.forward, forwardToPlayer), Is.GreaterThan(.999f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
