using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class FuelDepotPlannerTests
    {
        [Test]
        public void DepotsAreDeterministicAndReachableWithinMissionBounds()
        {
            var bounds = new Rect(-125f, -250f, 250f, 500f);
            var route = PayloadRoutePlanner.Generate(4711, bounds, 5, 8f);
            var first = FuelDepotPlanner.Generate(4711, bounds, route, 8);
            var second = FuelDepotPlanner.Generate(4711, bounds, route, 8);
            var exclusions = FuelDepotPlanner.BuildAccessExclusions(first);

            Assert.That(first, Has.Length.EqualTo(8));
            Assert.That(exclusions, Has.Length.EqualTo(8));
            for (var index = 0; index < first.Length; index++)
            {
                Assert.That(first[index].position, Is.EqualTo(second[index].position));
                Assert.That(bounds.Contains(new Vector2(first[index].position.x, first[index].position.z)), Is.True);
                Assert.That(Vector3.Distance(first[index].position, first[index].routeAnchor),
                    Is.InRange(10f, 25f));
                Assert.That(exclusions[index].Size.x, Is.GreaterThan(7f));
            }
        }
    }
}
