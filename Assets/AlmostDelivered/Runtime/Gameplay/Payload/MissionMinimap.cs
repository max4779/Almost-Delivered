using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class MissionMinimap : MonoBehaviour
    {
        private readonly List<RectTransform> routeSegments = new List<RectTransform>();
        private readonly List<RectTransform> checkpointMarkers = new List<RectTransform>();
        private readonly Dictionary<int, RectTransform> enemyIcons = new Dictionary<int, RectTransform>();
        private readonly List<int> staleEnemyIds = new List<int>();

        private Rect worldBounds;
        private Transform player;
        private Transform payload;
        private PayloadWaveSpawner waves;
        private RectTransform panel;

        public int RouteSegmentCount => routeSegments.Count;
        public int CheckpointMarkerCount => checkpointMarkers.Count;
        public int EnemyIconCount => enemyIcons.Count;
        public RectTransform PlayerIcon { get; private set; }
        public RectTransform PayloadIcon { get; private set; }

        public static Vector2 ProjectWorld(Vector3 worldPosition, Rect worldBounds, Rect minimapRect)
        {
            if (!IsFinitePositive(worldBounds.width) || !IsFinitePositive(worldBounds.height))
            {
                return minimapRect.center;
            }

            var normalizedX = Mathf.Clamp01((worldPosition.x - worldBounds.xMin) / worldBounds.width);
            var normalizedY = Mathf.Clamp01((worldPosition.z - worldBounds.yMin) / worldBounds.height);
            return new Vector2(
                Mathf.Lerp(minimapRect.xMin, minimapRect.xMax, normalizedX),
                Mathf.Lerp(minimapRect.yMin, minimapRect.yMax, normalizedY));
        }

        public void Configure(
            Rect bounds,
            PayloadRoutePlan route,
            Transform playerTarget,
            Transform payloadTarget,
            PayloadMissionController mission,
            PayloadWaveSpawner waveSpawner)
        {
            ClearViews();
            worldBounds = bounds;
            player = playerTarget;
            payload = payloadTarget;
            waves = waveSpawner;
            panel = GetComponent<RectTransform>();
            if (panel == null)
            {
                panel = gameObject.AddComponent<RectTransform>();
            }

            if (route != null)
            {
                BuildRoute(route);
            }

            PlayerIcon = CreateIcon("Player Icon", new Color(.25f, .9f, 1f, 1f), 10f);
            PayloadIcon = CreateIcon("Payload Icon", new Color(1f, .75f, .15f, 1f), 12f);
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (panel == null)
            {
                return;
            }

            var mapRect = panel.rect;
            UpdateIcon(PlayerIcon, player, mapRect);
            UpdateIcon(PayloadIcon, payload, mapRect);
            RefreshEnemyIcons(mapRect);
        }

        public RectTransform GetEnemyIcon(int enemyId)
        {
            return enemyIcons.TryGetValue(enemyId, out var icon) ? icon : null;
        }

        private void BuildRoute(PayloadRoutePlan route)
        {
            var mapRect = panel.rect;
            for (var index = 1; index < route.Points.Count; index++)
            {
                var start = ProjectWorld(route.Points[index - 1], worldBounds, mapRect);
                var end = ProjectWorld(route.Points[index], worldBounds, mapRect);
                var segment = CreateIcon($"Route Segment {index}", new Color(.2f, .75f, .9f, .75f), 1f);
                var delta = end - start;
                segment.anchoredPosition = (start + end) * .5f;
                segment.sizeDelta = new Vector2(delta.magnitude, 3f);
                segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                routeSegments.Add(segment);
            }

            for (var index = 0; index < route.CheckpointDistances.Count; index++)
            {
                var marker = CreateIcon($"Checkpoint {index + 1}", new Color(1f, .9f, .25f, 1f), 7f);
                marker.anchoredPosition = ProjectWorld(route.EvaluateDistance(route.CheckpointDistances[index]), worldBounds, mapRect);
                checkpointMarkers.Add(marker);
            }

            var destination = CreateIcon("Destination", new Color(1f, .35f, .2f, 1f), 9f);
            destination.anchoredPosition = ProjectWorld(route.Destination, worldBounds, mapRect);
            checkpointMarkers.Add(destination);
        }

        private void RefreshEnemyIcons(Rect mapRect)
        {
            staleEnemyIds.Clear();
            foreach (var pair in enemyIcons)
            {
                staleEnemyIds.Add(pair.Key);
            }

            if (waves != null)
            {
                foreach (var enemy in waves.LivingEnemies)
                {
                    if (enemy == null || enemy.IsDefeated)
                    {
                        continue;
                    }

                    staleEnemyIds.Remove(enemy.EnemyId);
                    if (!enemyIcons.TryGetValue(enemy.EnemyId, out var icon))
                    {
                        icon = CreateIcon($"Enemy {enemy.EnemyId}", new Color(1f, .2f, .2f, 1f), 6f);
                        enemyIcons.Add(enemy.EnemyId, icon);
                    }

                    icon.anchoredPosition = ProjectWorld(enemy.transform.position, worldBounds, mapRect);
                }
            }

            foreach (var enemyId in staleEnemyIds)
            {
                var icon = enemyIcons[enemyId];
                enemyIcons.Remove(enemyId);
                DestroyView(icon);
            }
        }

        private void UpdateIcon(RectTransform icon, Transform target, Rect mapRect)
        {
            if (icon == null)
            {
                return;
            }

            icon.gameObject.SetActive(target != null);
            if (target != null)
            {
                icon.anchoredPosition = ProjectWorld(target.position, worldBounds, mapRect);
            }
        }

        private RectTransform CreateIcon(string viewName, Color color, float size)
        {
            var iconObject = new GameObject(viewName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var icon = iconObject.GetComponent<RectTransform>();
            icon.SetParent(transform, false);
            icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
            icon.pivot = new Vector2(.5f, .5f);
            icon.sizeDelta = Vector2.one * size;
            var image = iconObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return icon;
        }

        private void ClearViews()
        {
            foreach (var segment in routeSegments)
            {
                DestroyView(segment);
            }

            foreach (var marker in checkpointMarkers)
            {
                DestroyView(marker);
            }

            foreach (var icon in enemyIcons.Values)
            {
                DestroyView(icon);
            }

            DestroyView(PlayerIcon);
            DestroyView(PayloadIcon);
            routeSegments.Clear();
            checkpointMarkers.Clear();
            enemyIcons.Clear();
            PlayerIcon = null;
            PayloadIcon = null;
        }

        private static void DestroyView(RectTransform view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
