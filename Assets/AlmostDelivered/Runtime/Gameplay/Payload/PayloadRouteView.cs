using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PayloadRouteView : MonoBehaviour
    {
        private readonly List<Transform> checkpointMarkers = new List<Transform>();

        public IReadOnlyList<Transform> CheckpointMarkers => checkpointMarkers;

        public void Configure(PayloadRoutePlan route, Material lineMaterial)
        {
            ClearMarkers();
            var line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = .18f;
            line.startColor = new Color(.15f, .9f, 1f, .8f);
            line.endColor = new Color(1f, .7f, .15f, .9f);
            if (lineMaterial != null)
            {
                line.sharedMaterial = lineMaterial;
            }

            if (route == null)
            {
                line.positionCount = 0;
                return;
            }

            line.positionCount = route.Points.Count;
            for (var index = 0; index < route.Points.Count; index++)
            {
                line.SetPosition(index, route.Points[index] + Vector3.up * .08f);
            }

            for (var index = 0; index < route.CheckpointDistances.Count; index++)
            {
                CreateMarker($"Checkpoint {index + 1}", route.EvaluateDistance(route.CheckpointDistances[index]), .35f);
            }

            CreateMarker("Destination", route.Destination, .5f);
        }

        private void ClearMarkers()
        {
            foreach (var marker in checkpointMarkers)
            {
                if (marker == null)
                {
                    continue;
                }

                marker.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(marker.gameObject);
                }
                else
                {
                    DestroyImmediate(marker.gameObject);
                }
            }

            checkpointMarkers.Clear();
        }

        private void CreateMarker(string markerName, Vector3 position, float scale)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = markerName;
            marker.transform.SetParent(transform, true);
            marker.transform.position = position + Vector3.up * .08f;
            marker.transform.localScale = new Vector3(scale, .08f, scale);
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            checkpointMarkers.Add(marker.transform);
        }
    }
}
