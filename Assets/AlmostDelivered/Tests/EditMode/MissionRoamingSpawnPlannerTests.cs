using System.Linq;
using AlmostDelivered.Gameplay.Payload;
using AlmostDelivered.MapGeneration;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class MissionRoamingSpawnPlannerTests
    {
        [Test]
        public void SeededRoamersStayAwayFromPlayerCargoObjectivesAndEachOther()
        {
            var bounds = new Rect(-125f, -250f, 250f, 500f);
            var route = PayloadRoutePlanner.Generate(4711, bounds, 5, 8f);
            var start = route.EvaluateDistance(58f) + Vector3.right * 16f;
            var objectives = route.CheckpointDistances.Select(route.EvaluateDistance).ToArray();
            var blockers = new[] { new MapExclusionZone(new Vector2(0f, 0f), new Vector2(30f, 30f)) };

            var first = MissionRoamingSpawnPlanner.Generate(4711, bounds, route, start, objectives, blockers, 8);
            var second = MissionRoamingSpawnPlanner.Generate(4711, bounds, route, start, objectives, blockers, 8);

            Assert.That(first, Has.Length.EqualTo(8));
            Assert.That(first, Is.EqualTo(second));
            for (var index = 0; index < first.Length; index++)
            {
                var point = first[index];
                Assert.That(bounds.Contains(new Vector2(point.x, point.z)), Is.True);
                Assert.That(Vector3.Distance(point, start), Is.GreaterThanOrEqualTo(45f));
                Assert.That(Vector3.Distance(point, route.Points[0]), Is.GreaterThanOrEqualTo(28f));
                Assert.That(objectives.All(target => Vector3.Distance(point, target) >= 18f), Is.True);
                Assert.That(Mathf.Abs(point.x) >= 17f || Mathf.Abs(point.z) >= 17f, Is.True);
                for (var other = 0; other < index; other++)
                    Assert.That(Vector3.Distance(point, first[other]), Is.GreaterThanOrEqualTo(19f));
            }
        }
    }
}
