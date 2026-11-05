using System;
using System.Collections.Generic;
using AlmostDelivered.MapGeneration;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class ProceduralMapPlannerTests
    {
        private static readonly Vector2 MapHalfExtents = new Vector2(20f, 16f);

        [Test]
        public void IdenticalSeedsProduceIdenticalPlacements()
        {
            var pieces = Pieces(new Vector2(2f, 4f), new Vector2(3f, 3f));

            var first = ProceduralMapPlanner.Generate(4711, MapHalfExtents, pieces, 10, NoExclusions(), 400);
            var second = ProceduralMapPlanner.Generate(4711, MapHalfExtents, pieces, 10, NoExclusions(), 400);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].PieceIndex, Is.EqualTo(first[index].PieceIndex));
                Assert.That(second[index].Position, Is.EqualTo(first[index].Position));
                Assert.That(second[index].QuarterTurns, Is.EqualTo(first[index].QuarterTurns));
                Assert.That(second[index].Footprint, Is.EqualTo(first[index].Footprint));
            }
        }

        [Test]
        public void DifferentSeedsVaryTheLayout()
        {
            var pieces = Pieces(new Vector2(2f, 4f), new Vector2(3f, 3f));

            var first = ProceduralMapPlanner.Generate(11, MapHalfExtents, pieces, 8, NoExclusions(), 400);
            var second = ProceduralMapPlanner.Generate(29, MapHalfExtents, pieces, 8, NoExclusions(), 400);

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.Not.Empty);
            Assert.That(LayoutsMatch(first, second), Is.False);
        }

        [Test]
        public void PlacementsStayInsideBounds()
        {
            var placements = ProceduralMapPlanner.Generate(
                123,
                new Vector2(7f, 5f),
                Pieces(new Vector2(2f, 4f), new Vector2(3f, 1f)),
                12,
                NoExclusions(),
                800);

            Assert.That(placements, Is.Not.Empty);
            foreach (var placement in placements)
            {
                Assert.That(Mathf.Abs(placement.Position.x) + placement.Footprint.x * .5f, Is.LessThanOrEqualTo(7.0001f));
                Assert.That(Mathf.Abs(placement.Position.y) + placement.Footprint.y * .5f, Is.LessThanOrEqualTo(5.0001f));
            }
        }

        [Test]
        public void PlacementsAvoidExclusionsAndEachOther()
        {
            var exclusion = new MapExclusionZone(Vector2.zero, new Vector2(8f, 6f));
            var placements = ProceduralMapPlanner.Generate(
                90210,
                MapHalfExtents,
                Pieces(new Vector2(2f, 4f), new Vector2(3f, 2f)),
                18,
                new[] { exclusion },
                1200);

            Assert.That(placements.Count, Is.GreaterThan(4));
            for (var index = 0; index < placements.Count; index++)
            {
                Assert.That(Overlaps(placements[index].Position, placements[index].Footprint, exclusion.Center, exclusion.Size), Is.False);
                for (var other = index + 1; other < placements.Count; other++)
                {
                    Assert.That(
                        Overlaps(placements[index].Position, placements[index].Footprint, placements[other].Position, placements[other].Footprint),
                        Is.False,
                        $"Placements {index} and {other} overlap.");
                }
            }
        }

        [Test]
        public void InvalidFootprintsAreIgnored()
        {
            var pieces = Pieces(
                Vector2.zero,
                new Vector2(-1f, 2f),
                new Vector2(float.NaN, 2f),
                new Vector2(2f, 2f));

            var placements = ProceduralMapPlanner.Generate(5, MapHalfExtents, pieces, 6, NoExclusions(), 300);

            Assert.That(placements, Is.Not.Empty);
            Assert.That(placements, Has.All.Matches<MapPlacement>(placement => placement.PieceIndex == 3));
        }

        [Test]
        public void ZeroCountOrAttemptBudgetProducesNoPlacements()
        {
            var pieces = Pieces(new Vector2(2f, 2f));

            Assert.That(ProceduralMapPlanner.Generate(1, MapHalfExtents, pieces, 0, NoExclusions(), 50), Is.Empty);
            Assert.That(ProceduralMapPlanner.Generate(1, MapHalfExtents, pieces, 5, NoExclusions(), 0), Is.Empty);
        }

        [Test]
        public void AttemptBudgetTerminatesWhenMapIsFull()
        {
            var placements = ProceduralMapPlanner.Generate(
                42,
                Vector2.one,
                Pieces(new Vector2(2f, 2f)),
                10,
                NoExclusions(),
                5);

            Assert.That(placements, Has.Count.EqualTo(1));
        }

        private static IReadOnlyList<MapPieceSpec> Pieces(params Vector2[] footprints)
        {
            var pieces = new List<MapPieceSpec>(footprints.Length);
            foreach (var footprint in footprints)
            {
                pieces.Add(new MapPieceSpec(footprint));
            }

            return pieces;
        }

        private static IReadOnlyList<MapExclusionZone> NoExclusions()
        {
            return Array.Empty<MapExclusionZone>();
        }

        private static bool LayoutsMatch(IReadOnlyList<MapPlacement> first, IReadOnlyList<MapPlacement> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            for (var index = 0; index < first.Count; index++)
            {
                if (first[index].PieceIndex != second[index].PieceIndex ||
                    first[index].Position != second[index].Position ||
                    first[index].QuarterTurns != second[index].QuarterTurns)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Overlaps(Vector2 firstCenter, Vector2 firstSize, Vector2 secondCenter, Vector2 secondSize)
        {
            return Mathf.Abs(firstCenter.x - secondCenter.x) < (firstSize.x + secondSize.x) * .5f &&
                   Mathf.Abs(firstCenter.y - secondCenter.y) < (firstSize.y + secondSize.y) * .5f;
        }
    }
}
