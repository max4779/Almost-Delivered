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
            Assert.That(coreWidths, Has.All.InRange(5f, 6f));
            Assert.That(coreWidths.Max() - coreWidths.Min(), Is.GreaterThan(.1f));
            Assert.That(road.RoadMesh.GetTriangles(0), Is.Not.Empty);
            Assert.That(road.RoadMesh.GetTriangles(1), Is.Not.Empty);
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
    }
}
