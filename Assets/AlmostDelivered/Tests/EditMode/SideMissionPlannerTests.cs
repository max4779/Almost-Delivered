using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class SideMissionPlannerTests
    {
        private static readonly Rect Bounds = new Rect(-50f, -90f, 100f, 180f);

        [Test]
        public void OneRunPlacesBothObjectivesInReachableSideAreas()
        {
            var route = PayloadRoutePlanner.Generate(4711, Bounds, 5, 8f);
            var placements = SideMissionPlanner.Generate(4711, Bounds, route);
            var exclusions = SideMissionPlanner.BuildAccessExclusions(placements);

            Assert.That(placements, Has.Length.EqualTo(2));
            Assert.That(placements[0].kind, Is.Not.EqualTo(placements[1].kind));
            Assert.That(exclusions, Has.Length.EqualTo(4));
            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                Assert.That(Bounds.Contains(new Vector2(placement.position.x, placement.position.z)), Is.True);
                Assert.That(Vector3.Distance(placement.position, placement.routeAnchor), Is.GreaterThan(20f));
                var branch = exclusions[index * 2 + 1];
                AssertInside(branch, placement.position);
                AssertInside(branch, placement.routeAnchor);
            }
        }

        [Test]
        public void SameSeedKeepsObjectivesAndLocationsStableForAllPlayers()
        {
            var route = PayloadRoutePlanner.Generate(1937, Bounds, 5, 8f);
            var first = SideMissionPlanner.Generate(1937, Bounds, route);
            var second = SideMissionPlanner.Generate(1937, Bounds, route);

            for (var index = 0; index < first.Length; index++)
            {
                Assert.That(second[index].kind, Is.EqualTo(first[index].kind));
                Assert.That(second[index].position, Is.EqualTo(first[index].position));
                Assert.That(second[index].routeAnchor, Is.EqualTo(first[index].routeAnchor));
            }
        }

        private static void AssertInside(AlmostDelivered.MapGeneration.MapExclusionZone zone, Vector3 point)
        {
            Assert.That(Mathf.Abs(point.x - zone.Center.x), Is.LessThanOrEqualTo(zone.Size.x * .5f));
            Assert.That(Mathf.Abs(point.z - zone.Center.y), Is.LessThanOrEqualTo(zone.Size.y * .5f));
        }
    }
}
