using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionMinimapProjectionTests
    {
        private static readonly Rect WorldBounds = new Rect(-10f, -20f, 20f, 40f);
        private static readonly Rect MinimapRect = new Rect(0f, 0f, 200f, 100f);

        [TestCase(-10f, -20f, 0f, 0f)]
        [TestCase(10f, -20f, 200f, 0f)]
        [TestCase(-10f, 20f, 0f, 100f)]
        [TestCase(10f, 20f, 200f, 100f)]
        [TestCase(0f, 0f, 100f, 50f)]
        public void ProjectsWorldCornersAndCenter(float x, float z, float expectedX, float expectedY)
        {
            var projected = MissionMinimap.ProjectWorld(new Vector3(x, 7f, z), WorldBounds, MinimapRect);

            Assert.That(projected.x, Is.EqualTo(expectedX).Within(.0001f));
            Assert.That(projected.y, Is.EqualTo(expectedY).Within(.0001f));
        }

        [Test]
        public void ProjectionClampsPositionsOutsideWorldBounds()
        {
            Assert.That(
                MissionMinimap.ProjectWorld(new Vector3(-100f, 0f, 100f), WorldBounds, MinimapRect),
                Is.EqualTo(new Vector2(0f, 100f)));
            Assert.That(
                MissionMinimap.ProjectWorld(new Vector3(100f, 0f, -100f), WorldBounds, MinimapRect),
                Is.EqualTo(new Vector2(200f, 0f)));
        }

        [Test]
        public void NonSquareBoundsUseIndependentAxes()
        {
            var projected = MissionMinimap.ProjectWorld(
                new Vector3(25f, 0f, 2.5f),
                new Rect(0f, 0f, 100f, 10f),
                new Rect(-50f, -25f, 400f, 200f));

            Assert.That(projected, Is.EqualTo(new Vector2(50f, 25f)));
        }

        [TestCase(0f, 10f)]
        [TestCase(10f, 0f)]
        public void InvalidWorldBoundsReturnMinimapCenter(float width, float height)
        {
            var rect = new Rect(10f, 20f, 80f, 40f);

            var projected = MissionMinimap.ProjectWorld(Vector3.zero, new Rect(0f, 0f, width, height), rect);

            Assert.That(projected, Is.EqualTo(rect.center));
        }
    }
}
