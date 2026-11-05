using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class PayloadRoutePlannerTests
    {
        private static readonly Rect MissionBounds = new Rect(-28f, -50f, 56f, 100f);

        [Test]
        public void IdenticalSeedsProduceIdenticalRoutes()
        {
            var first = PayloadRoutePlanner.Generate(4711, MissionBounds, 5, 3f);
            var second = PayloadRoutePlanner.Generate(4711, MissionBounds, 5, 3f);

            Assert.That(second.Points, Is.EqualTo(first.Points));
            Assert.That(second.CumulativeDistances, Is.EqualTo(first.CumulativeDistances));
            Assert.That(second.CheckpointDistances, Is.EqualTo(first.CheckpointDistances));
            Assert.That(second.TotalLength, Is.EqualTo(first.TotalLength));
        }

        [Test]
        public void DifferentSeedsVaryAtLeastOneInteriorPoint()
        {
            var first = PayloadRoutePlanner.Generate(11, MissionBounds, 5, 3f);
            var second = PayloadRoutePlanner.Generate(29, MissionBounds, 5, 3f);

            Assert.That(RoutesMatch(first, second), Is.False);
        }

        [Test]
        public void NormalMissionRouteHasSevenOrderedBoundedPointsAndTargetLength()
        {
            var route = PayloadRoutePlanner.Generate(90210, MissionBounds, 5, 3f);

            Assert.That(route.Points, Has.Count.EqualTo(7));
            Assert.That(route.TotalLength, Is.InRange(120f, 160f));
            for (var index = 0; index < route.Points.Count; index++)
            {
                var point = route.Points[index];
                Assert.That(point.x, Is.InRange(MissionBounds.xMin, MissionBounds.xMax));
                Assert.That(point.z, Is.InRange(MissionBounds.yMin, MissionBounds.yMax));
                Assert.That(point.y, Is.EqualTo(0f));

                if (index > 0)
                {
                    Assert.That(point.z, Is.GreaterThan(route.Points[index - 1].z));
                    Assert.That(route.CumulativeDistances[index], Is.GreaterThan(route.CumulativeDistances[index - 1]));
                }
            }
        }

        [Test]
        public void CheckpointsAreQuarterSamplesOfTotalDistance()
        {
            var route = PayloadRoutePlanner.Generate(123, MissionBounds, 5, 3f);

            Assert.That(route.CheckpointDistances, Has.Count.EqualTo(3));
            Assert.That(route.CheckpointDistances[0], Is.EqualTo(route.TotalLength * .25f).Within(.0001f));
            Assert.That(route.CheckpointDistances[1], Is.EqualTo(route.TotalLength * .5f).Within(.0001f));
            Assert.That(route.CheckpointDistances[2], Is.EqualTo(route.TotalLength * .75f).Within(.0001f));
        }

        [Test]
        public void DistanceSamplingClampsToRouteEndpoints()
        {
            var route = PayloadRoutePlanner.Generate(8080, MissionBounds, 5, 3f);

            Assert.That(route.EvaluateDistance(-10f), Is.EqualTo(route.Points[0]));
            Assert.That(route.EvaluateDistance(0f), Is.EqualTo(route.Points[0]));
            Assert.That(route.EvaluateDistance(route.TotalLength), Is.EqualTo(route.Destination));
            Assert.That(route.EvaluateDistance(route.TotalLength + 10f), Is.EqualTo(route.Destination));
        }

        [Test]
        public void DistanceSamplingInterpolatesInsideItsOwningSegment()
        {
            var route = PayloadRoutePlanner.Generate(303, MissionBounds, 5, 3f);
            var segmentStart = route.CumulativeDistances[2];
            var segmentEnd = route.CumulativeDistances[3];
            var expected = Vector3.Lerp(route.Points[2], route.Points[3], .5f);

            var sampled = route.EvaluateDistance((segmentStart + segmentEnd) * .5f);

            Assert.That(sampled.x, Is.EqualTo(expected.x).Within(.0001f));
            Assert.That(sampled.z, Is.EqualTo(expected.z).Within(.0001f));
        }

        [Test]
        public void CorridorExclusionsContainBothEndsOfEverySegment()
        {
            const float corridorWidth = 4f;
            var route = PayloadRoutePlanner.Generate(77, MissionBounds, 5, corridorWidth);

            Assert.That(route.CorridorExclusions, Has.Count.EqualTo(route.Points.Count - 1));
            for (var index = 0; index < route.CorridorExclusions.Count; index++)
            {
                var zone = route.CorridorExclusions[index];
                AssertPointInside(zone.Center, zone.Size, route.Points[index]);
                AssertPointInside(zone.Center, zone.Size, route.Points[index + 1]);
                Assert.That(zone.Size.x, Is.GreaterThanOrEqualTo(corridorWidth));
                Assert.That(zone.Size.y, Is.GreaterThanOrEqualTo(corridorWidth));
            }
        }

        [TestCase(0f, 100f)]
        [TestCase(56f, 0f)]
        [TestCase(-1f, 100f)]
        public void InvalidBoundsProduceFiniteStationaryRoute(float width, float height)
        {
            var route = PayloadRoutePlanner.Generate(1, new Rect(0f, 0f, width, height), 5, 3f);

            Assert.That(route.Points, Has.Count.EqualTo(1));
            Assert.That(route.TotalLength, Is.EqualTo(0f));
            Assert.That(float.IsNaN(route.Destination.x) || float.IsInfinity(route.Destination.x), Is.False);
            Assert.That(float.IsNaN(route.Destination.z) || float.IsInfinity(route.Destination.z), Is.False);
            Assert.That(route.CorridorExclusions, Is.Empty);
            Assert.That(route.EvaluateDistance(float.NaN), Is.EqualTo(route.Destination));
        }

        [Test]
        public void NonFiniteBoundsProduceFiniteStationaryRoute()
        {
            var route = PayloadRoutePlanner.Generate(1, new Rect(float.NaN, 0f, 56f, 100f), 5, 3f);

            Assert.That(route.TotalLength, Is.EqualTo(0f));
            Assert.That(route.Destination, Is.EqualTo(Vector3.zero));
        }

        private static bool RoutesMatch(PayloadRoutePlan first, PayloadRoutePlan second)
        {
            if (first.Points.Count != second.Points.Count)
            {
                return false;
            }

            for (var index = 0; index < first.Points.Count; index++)
            {
                if (first.Points[index] != second.Points[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertPointInside(Vector2 center, Vector2 size, Vector3 point)
        {
            Assert.That(Mathf.Abs(point.x - center.x), Is.LessThanOrEqualTo(size.x * .5f + .0001f));
            Assert.That(Mathf.Abs(point.z - center.y), Is.LessThanOrEqualTo(size.y * .5f + .0001f));
        }
    }
}
