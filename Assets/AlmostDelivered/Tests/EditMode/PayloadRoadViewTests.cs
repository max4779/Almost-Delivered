using System.Collections.Generic;
using System.Linq;
using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class PayloadRoadViewTests
    {
        private readonly Rect bounds = new Rect(-28f, -50f, 56f, 100f);
        private Material earth;
        private Material moss;
        private GameObject grassPrefab;

        [SetUp]
        public void SetUp()
        {
            earth = new Material(Shader.Find("Standard"));
            moss = new Material(Shader.Find("Standard"));
            grassPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grassPrefab.name = "Test Grass";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(grassPrefab);
            Object.DestroyImmediate(earth);
            Object.DestroyImmediate(moss);
            foreach (var road in Object.FindObjectsOfType<PayloadRoadView>(true))
            {
                Object.DestroyImmediate(road.gameObject);
            }
        }

        [Test]
        public void IdenticalInputsProduceIdenticalTwoMaterialRoadMeshes()
        {
            var route = PayloadRoutePlanner.Generate(4711, bounds, 5, 8f);
            var first = CreateRoad("First", route, 4711);
            var second = CreateRoad("Second", route, 4711);

            Assert.That(second.RoadMesh.vertices, Is.EqualTo(first.RoadMesh.vertices));
            Assert.That(second.RoadMesh.triangles, Is.EqualTo(first.RoadMesh.triangles));
            Assert.That(first.RoadMesh.subMeshCount, Is.EqualTo(2));
            Assert.That(first.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(new[] { earth, moss }));
        }

        [Test]
        public void RoadUsesNominalWidthWithIrregularEarthEdgesAndMossShoulders()
        {
            var route = PayloadRoutePlanner.Generate(90210, bounds, 5, 8f);
            var road = CreateRoad("Road", route, 90210);
            var vertices = road.RoadMesh.vertices;
            var coreWidths = Enumerable.Range(0, vertices.Length / 4)
                .Select(index => Vector3.Distance(vertices[index * 4 + 1], vertices[index * 4 + 2]))
                .ToArray();

            Assert.That(road.RoadWidth, Is.EqualTo(5.5f));
            Assert.That(coreWidths.Count(width => width >= 5f && width <= 6f),
                Is.GreaterThan(coreWidths.Length * .9f));
            Assert.That(coreWidths.Max(), Is.LessThan(9f));
            Assert.That(coreWidths.Max() - coreWidths.Min(), Is.GreaterThan(.1f));
            Assert.That(road.RoadMesh.GetTriangles(0), Is.Not.Empty);
            Assert.That(road.RoadMesh.GetTriangles(1), Is.Not.Empty);
        }

        [Test]
        public void BentRoadKeepsTrianglesUpwardAndMaterialStripsFromOverlapping()
        {
            var route = PayloadRoutePlanner.Generate(4711, bounds, 5, 8f);
            var road = CreateRoad("Road", route, 4711);
            var mesh = road.RoadMesh;

            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                var triangles = mesh.GetTriangles(subMesh);
                for (var index = 0; index < triangles.Length; index += 3)
                {
                    var a = mesh.vertices[triangles[index]];
                    var b = mesh.vertices[triangles[index + 1]];
                    var c = mesh.vertices[triangles[index + 2]];
                    Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(.00001f),
                        $"Submesh {subMesh} triangle {index / 3} must face upward: a={a}, b={b}, c={c}.");
                }
            }

            var earthTriangles = mesh.GetTriangles(0);
            var mossTriangles = mesh.GetTriangles(1);
            for (var earthIndex = 0; earthIndex < earthTriangles.Length; earthIndex += 3)
            for (var mossIndex = 0; mossIndex < mossTriangles.Length; mossIndex += 3)
            {
                var overlap = TriangleOverlapArea(
                    mesh.vertices[earthTriangles[earthIndex]],
                    mesh.vertices[earthTriangles[earthIndex + 1]],
                    mesh.vertices[earthTriangles[earthIndex + 2]],
                    mesh.vertices[mossTriangles[mossIndex]],
                    mesh.vertices[mossTriangles[mossIndex + 1]],
                    mesh.vertices[mossTriangles[mossIndex + 2]]);
                Assert.That(overlap, Is.LessThan(.00001f),
                    $"Earth triangle {earthIndex / 3} overlaps moss triangle {mossIndex / 3}.");
            }
        }

        [Test]
        public void VeryShortRouteProducesFiniteUpwardGeometry()
        {
            var route = PayloadRoutePlanner.Generate(7, new Rect(-1f, -.1f, 2f, .2f), 0, .1f);
            var road = CreateRoad("Short Road", route, 7);

            Assert.That(road.RoadMesh.vertexCount, Is.EqualTo(8));
            Assert.That(road.RoadMesh.vertices.All(IsFinite), Is.True);
            Assert.That(road.RoadMesh.normals.All(normal => IsFinite(normal) && normal.y > 0f), Is.True);
        }

        [Test]
        public void RoadsideFoliageIsSparseDeterministicAndColliderFree()
        {
            var route = PayloadRoutePlanner.Generate(4711, bounds, 5, 8f);
            var first = CreateRoad("First", route, 4711);
            var second = CreateRoad("Second", route, 4711);

            Assert.That(first.RoadsideFoliageCount, Is.InRange(8, 30));
            Assert.That(second.RoadsideFoliageCount, Is.EqualTo(first.RoadsideFoliageCount));
            Assert.That(first.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(
                second.GetComponentsInChildren<Transform>(true).Select(item => item.localPosition),
                Is.EqualTo(first.GetComponentsInChildren<Transform>(true).Select(item => item.localPosition)));
        }

        [Test]
        public void NullAndStationaryRoutesProduceNoGeometryOrFoliage()
        {
            var road = CreateRoad("Road", null, 1);

            Assert.That(road.RoadMesh.vertexCount, Is.EqualTo(0));
            Assert.That(road.RoadsideFoliageCount, Is.EqualTo(0));

            var stationary = PayloadRoutePlanner.Generate(1, new Rect(0f, 0f, 0f, 100f), 5, 8f);
            road.Configure(stationary, 1, 5.5f, earth, moss, new[] { grassPrefab });

            Assert.That(road.RoadMesh.vertexCount, Is.EqualTo(0));
            Assert.That(road.RoadsideFoliageCount, Is.EqualTo(0));
        }

        [Test]
        public void ReconfigureReplacesPriorMeshAndFoliage()
        {
            var road = CreateRoad("Road", PayloadRoutePlanner.Generate(11, bounds, 5, 8f), 11);
            var firstMesh = road.RoadMesh;
            var firstFoliageCount = road.RoadsideFoliageCount;

            road.Configure(PayloadRoutePlanner.Generate(29, bounds, 5, 8f), 29, 5.5f, earth, moss, new[] { grassPrefab });

            Assert.That(road.RoadMesh, Is.Not.SameAs(firstMesh));
            Assert.That(road.RoadsideFoliageCount, Is.EqualTo(firstFoliageCount));
            Assert.That(road.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        private PayloadRoadView CreateRoad(string name, PayloadRoutePlan route, int seed)
        {
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(PayloadRoadView));
            var road = root.GetComponent<PayloadRoadView>();
            road.Configure(route, seed, 5.5f, earth, moss, new[] { grassPrefab });
            return road;
        }

        private static float TriangleOverlapArea(
            Vector3 firstA,
            Vector3 firstB,
            Vector3 firstC,
            Vector3 secondA,
            Vector3 secondB,
            Vector3 secondC)
        {
            var polygon = new List<Vector2>
            {
                ToPlanar(firstA),
                ToPlanar(firstB),
                ToPlanar(firstC)
            };
            var clip = new[] { ToPlanar(secondA), ToPlanar(secondB), ToPlanar(secondC) };
            var orientation = Mathf.Sign(Cross(clip[1] - clip[0], clip[2] - clip[0]));
            for (var edgeIndex = 0; edgeIndex < 3 && polygon.Count > 0; edgeIndex++)
            {
                var edgeStart = clip[edgeIndex];
                var edgeEnd = clip[(edgeIndex + 1) % 3];
                var input = polygon;
                polygon = new List<Vector2>();
                var previous = input[input.Count - 1];
                var previousInside = IsInside(previous, edgeStart, edgeEnd, orientation);
                foreach (var current in input)
                {
                    var currentInside = IsInside(current, edgeStart, edgeEnd, orientation);
                    if (currentInside != previousInside)
                    {
                        polygon.Add(LineIntersection(previous, current, edgeStart, edgeEnd));
                    }

                    if (currentInside)
                    {
                        polygon.Add(current);
                    }

                    previous = current;
                    previousInside = currentInside;
                }
            }

            var twiceArea = 0f;
            for (var index = 0; index < polygon.Count; index++)
            {
                var next = polygon[(index + 1) % polygon.Count];
                twiceArea += Cross(polygon[index], next);
            }

            return Mathf.Abs(twiceArea) * .5f;
        }

        private static bool IsInside(Vector2 point, Vector2 edgeStart, Vector2 edgeEnd, float orientation)
        {
            return orientation * Cross(edgeEnd - edgeStart, point - edgeStart) >= -.000001f;
        }

        private static Vector2 LineIntersection(Vector2 start, Vector2 end, Vector2 lineStart, Vector2 lineEnd)
        {
            var movement = end - start;
            var line = lineEnd - lineStart;
            var denominator = Cross(movement, line);
            if (Mathf.Abs(denominator) <= .000001f)
            {
                return start;
            }

            var t = Cross(lineStart - start, line) / denominator;
            return start + movement * t;
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private static Vector2 ToPlanar(Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
