using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PayloadRoadView : MonoBehaviour
    {
        private const float SampleSpacing = 1.5f;
        private const float CornerClearance = 5f;
        private const float ShoulderWidth = .9f;
        private const float SurfaceHeight = .035f;
        private const float GrassSpacing = 7f;

        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform foliageRoot;
        [SerializeField] private float roadWidth;
        [SerializeField] private int roadsideFoliageCount;

        public Mesh RoadMesh => meshFilter != null ? meshFilter.sharedMesh : null;
        public float RoadWidth => roadWidth;
        public int RoadsideFoliageCount => roadsideFoliageCount;

        public void Configure(
            PayloadRoutePlan route,
            int seed,
            float width,
            Material earth,
            Material moss,
            GameObject[] grassPrefabs)
        {
            CacheComponents();
            ClearGeneratedContent();
            roadWidth = Mathf.Max(.1f, width);
            meshRenderer.sharedMaterials = new[] { earth, moss };

            var mesh = new Mesh { name = "Payload Road Mesh" };
            meshFilter.sharedMesh = mesh;
            if (route == null || route.Points.Count < 2 || route.TotalLength <= Mathf.Epsilon)
            {
                mesh.subMeshCount = 2;
                return;
            }

            var distances = BuildSampleDistances(route);
            BuildRoadMesh(mesh, route, seed, distances);
            BuildRoadsideFoliage(route, seed, grassPrefabs);
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private void ClearGeneratedContent()
        {
            if (foliageRoot != null)
            {
                DestroyObject(foliageRoot.gameObject);
                foliageRoot = null;
            }

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                DestroyObject(meshFilter.sharedMesh);
                meshFilter.sharedMesh = null;
            }

            roadsideFoliageCount = 0;
        }

        private static List<float> BuildSampleDistances(PayloadRoutePlan route)
        {
            var distances = new List<float>();
            for (var segment = 1; segment < route.CumulativeDistances.Count; segment++)
            {
                var start = route.CumulativeDistances[segment - 1];
                var end = route.CumulativeDistances[segment];
                distances.Add(start);
                var firstInterior = start + (segment == 1 ? SampleSpacing : CornerClearance);
                var lastInterior = end - (segment == route.CumulativeDistances.Count - 1
                    ? SampleSpacing
                    : CornerClearance);
                for (var distance = firstInterior; distance <= lastInterior + .001f; distance += SampleSpacing)
                {
                    distances.Add(distance);
                }
            }

            distances.Add(route.TotalLength);
            return distances;
        }

        private void BuildRoadMesh(Mesh mesh, PayloadRoutePlan route, int seed, IReadOnlyList<float> distances)
        {
            var vertices = new Vector3[distances.Count * 4];
            var uvs = new Vector2[vertices.Length];
            var earthTriangles = new int[(distances.Count - 1) * 6];
            var shoulderTriangles = new int[(distances.Count - 1) * 12];
            var centers = new Vector3[distances.Count];
            var leftHalves = new float[distances.Count];
            var rightHalves = new float[distances.Count];
            var leftShoulders = new float[distances.Count];
            var rightShoulders = new float[distances.Count];
            var noiseOrigin = SeedNoiseOrigin(seed);

            for (var index = 0; index < distances.Count; index++)
            {
                var distance = distances[index];
                var center = route.EvaluateDistance(distance);
                var tangent = TangentAt(route, distance);
                var lateral = new Vector3(-tangent.z, 0f, tangent.x);
                var centerNoise = SignedNoise(noiseOrigin, distance, .071f, .17f) * .24f;
                var leftNoise = SignedNoise(noiseOrigin + 19.7f, distance, .083f, .41f) * .22f;
                var rightNoise = SignedNoise(noiseOrigin + 43.2f, distance, .077f, .73f) * .22f;
                var shoulderNoise = SignedNoise(noiseOrigin + 71.9f, distance, .061f, .29f) * .12f;
                center += lateral * centerNoise;

                centers[index] = center;
                leftHalves[index] = roadWidth * .5f + leftNoise;
                rightHalves[index] = roadWidth * .5f + rightNoise;
                leftShoulders[index] = leftHalves[index] + ShoulderWidth + shoulderNoise;
                rightShoulders[index] = rightHalves[index] + ShoulderWidth - shoulderNoise;
            }

            for (var index = 0; index < distances.Count; index++)
            {
                var distance = distances[index];
                MiterAt(centers, index, out var lateral, out var miterScale);
                var vertex = index * 4;
                vertices[vertex] = ToLocal(centers[index] + lateral * (leftShoulders[index] * miterScale));
                vertices[vertex + 1] = ToLocal(centers[index] + lateral * (leftHalves[index] * miterScale));
                vertices[vertex + 2] = ToLocal(centers[index] - lateral * (rightHalves[index] * miterScale));
                vertices[vertex + 3] = ToLocal(centers[index] - lateral * (rightShoulders[index] * miterScale));
                var uvDistance = distance * .25f;
                uvs[vertex] = new Vector2(0f, uvDistance);
                uvs[vertex + 1] = new Vector2(.15f, uvDistance);
                uvs[vertex + 2] = new Vector2(.85f, uvDistance);
                uvs[vertex + 3] = new Vector2(1f, uvDistance);
            }

            for (var index = 0; index < distances.Count - 1; index++)
            {
                var current = index * 4;
                var next = current + 4;
                var earth = index * 6;
                earthTriangles[earth] = current + 1;
                earthTriangles[earth + 1] = next + 1;
                earthTriangles[earth + 2] = next + 2;
                earthTriangles[earth + 3] = current + 1;
                earthTriangles[earth + 4] = next + 2;
                earthTriangles[earth + 5] = current + 2;

                var shoulder = index * 12;
                shoulderTriangles[shoulder] = current;
                shoulderTriangles[shoulder + 1] = next;
                shoulderTriangles[shoulder + 2] = next + 1;
                shoulderTriangles[shoulder + 3] = current;
                shoulderTriangles[shoulder + 4] = next + 1;
                shoulderTriangles[shoulder + 5] = current + 1;
                shoulderTriangles[shoulder + 6] = current + 2;
                shoulderTriangles[shoulder + 7] = next + 2;
                shoulderTriangles[shoulder + 8] = next + 3;
                shoulderTriangles[shoulder + 9] = current + 2;
                shoulderTriangles[shoulder + 10] = next + 3;
                shoulderTriangles[shoulder + 11] = current + 3;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(earthTriangles, 0);
            mesh.SetTriangles(shoulderTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void MiterAt(IReadOnlyList<Vector3> centers, int index, out Vector3 lateral, out float scale)
        {
            var incoming = index > 0
                ? Vector3.ProjectOnPlane(centers[index] - centers[index - 1], Vector3.up).normalized
                : Vector3.zero;
            var outgoing = index < centers.Count - 1
                ? Vector3.ProjectOnPlane(centers[index + 1] - centers[index], Vector3.up).normalized
                : Vector3.zero;

            if (incoming.sqrMagnitude <= .0001f)
            {
                incoming = outgoing.sqrMagnitude > .0001f ? outgoing : Vector3.forward;
            }

            if (outgoing.sqrMagnitude <= .0001f)
            {
                outgoing = incoming;
            }

            var incomingLeft = new Vector3(-incoming.z, 0f, incoming.x);
            var outgoingLeft = new Vector3(-outgoing.z, 0f, outgoing.x);
            var combined = incomingLeft + outgoingLeft;
            if (combined.sqrMagnitude <= .0001f)
            {
                lateral = outgoingLeft;
                scale = 1f;
                return;
            }

            lateral = combined.normalized;
            var denominator = Vector3.Dot(lateral, outgoingLeft);
            if (denominator <= .25f)
            {
                lateral = outgoingLeft;
                scale = 1f;
                return;
            }

            scale = Mathf.Min(1f / denominator, 2f);
        }

        private void BuildRoadsideFoliage(PayloadRoutePlan route, int seed, IReadOnlyList<GameObject> grassPrefabs)
        {
            if (grassPrefabs == null || grassPrefabs.Count == 0)
            {
                return;
            }

            var usablePrefabs = new List<GameObject>(grassPrefabs.Count);
            for (var index = 0; index < grassPrefabs.Count; index++)
            {
                if (grassPrefabs[index] != null)
                {
                    usablePrefabs.Add(grassPrefabs[index]);
                }
            }

            if (usablePrefabs.Count == 0)
            {
                return;
            }

            var foliageObject = new GameObject("Roadside Grass");
            foliageRoot = foliageObject.transform;
            foliageRoot.SetParent(transform, false);
            var random = new System.Random(seed);
            for (var distance = 4f; distance < route.TotalLength - 3f; distance += GrassSpacing)
            {
                var center = route.EvaluateDistance(distance);
                var tangent = TangentAt(route, distance);
                var lateral = new Vector3(-tangent.z, 0f, tangent.x);
                var side = (roadsideFoliageCount + (seed & 1)) % 2 == 0 ? -1f : 1f;
                var offset = roadWidth * .5f + ShoulderWidth + 1f + (float)random.NextDouble() * .65f;
                var instance = Instantiate(usablePrefabs[roadsideFoliageCount % usablePrefabs.Count], foliageRoot);
                instance.name = $"Roadside Grass {roadsideFoliageCount + 1}";
                instance.transform.position = center + lateral * (side * offset) + Vector3.up * SurfaceHeight;
                instance.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up) *
                                              Quaternion.Euler(0f, (float)random.NextDouble() * 70f - 35f, 0f);
                var scale = Mathf.Lerp(.78f, 1.12f, (float)random.NextDouble());
                instance.transform.localScale *= scale;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    DestroyObject(collider);
                }

                roadsideFoliageCount++;
            }
        }

        private Vector3 ToLocal(Vector3 worldPosition)
        {
            worldPosition.y = SurfaceHeight;
            return transform.InverseTransformPoint(worldPosition);
        }

        private static Vector3 TangentAt(PayloadRoutePlan route, float distance)
        {
            var before = route.EvaluateDistance(Mathf.Max(0f, distance - SampleSpacing));
            var after = route.EvaluateDistance(Mathf.Min(route.TotalLength, distance + SampleSpacing));
            var tangent = Vector3.ProjectOnPlane(after - before, Vector3.up).normalized;
            return tangent.sqrMagnitude > .0001f ? tangent : Vector3.forward;
        }

        private static float SeedNoiseOrigin(int seed)
        {
            return (uint)seed * (1f / uint.MaxValue) * 997f;
        }

        private static float SignedNoise(float origin, float distance, float frequency, float lane)
        {
            return Mathf.PerlinNoise(origin + distance * frequency, lane) * 2f - 1f;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
