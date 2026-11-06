using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class MissionMinimap : MonoBehaviour
    {
        public const float ContentPadding = 10f;
        [SerializeField] private List<RectTransform> routeSegments = new List<RectTransform>();
        [SerializeField] private List<RectTransform> checkpointMarkers = new List<RectTransform>();
        private readonly List<float> routeSegmentEndDistances = new List<float>();
        private readonly Dictionary<int, RectTransform> enemyIcons = new Dictionary<int, RectTransform>();
        private readonly List<int> staleEnemyIds = new List<int>();

        [SerializeField] private Rect worldBounds;
        [SerializeField] private Transform player;
        [SerializeField] private Transform payload;
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private PayloadWaveSpawner waves;
        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform playerIcon;
        [SerializeField] private RectTransform payloadIcon;
        private PayloadRoutePlan routePlan;

        private static readonly Color CurrentRouteColor = new Color(.2f, .85f, 1f, .95f);
        private static readonly Color FutureRouteColor = new Color(.15f, .38f, .48f, .28f);
        private static readonly Color CompletedRouteColor = new Color(.2f, .65f, .4f, .6f);
        private static readonly Color ActiveCheckpointColor = new Color(1f, .9f, .25f, 1f);
        private static readonly Color FutureCheckpointColor = new Color(.22f, .38f, .46f, .45f);
        private static readonly Color CompletedCheckpointColor = new Color(.2f, .8f, .35f, .85f);

        public int RouteSegmentCount => routeSegments.Count;
        public int CheckpointMarkerCount => checkpointMarkers.Count;
        public int EnemyIconCount => enemyIcons.Count;
        public RectTransform PlayerIcon => playerIcon;
        public RectTransform PayloadIcon => payloadIcon;

        public static Vector2 ProjectWorld(Vector3 worldPosition, Rect worldBounds, Rect minimapRect)
        {
            return ProjectWorldInsidePanel(worldPosition, worldBounds, minimapRect, 0f);
        }

        public static Vector2 ProjectWorldInsidePanel(Vector3 worldPosition, Rect worldBounds, Rect minimapRect, float padding = ContentPadding)
        {
            if (!IsFinitePositive(worldBounds.width) || !IsFinitePositive(worldBounds.height))
            {
                return minimapRect.center;
            }

            var contentRect = Inset(minimapRect, Mathf.Max(0f, padding));
            var normalizedX = Mathf.Clamp01((worldPosition.x - worldBounds.xMin) / worldBounds.width);
            var normalizedY = Mathf.Clamp01((worldPosition.z - worldBounds.yMin) / worldBounds.height);
            return new Vector2(
                Mathf.Lerp(contentRect.xMin, contentRect.xMax, normalizedX),
                Mathf.Lerp(contentRect.yMin, contentRect.yMax, normalizedY));
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
            this.mission = mission;
            waves = waveSpawner;
            routePlan = route;
            panel = GetComponent<RectTransform>();
            if (panel == null)
            {
                panel = gameObject.AddComponent<RectTransform>();
            }
            if (GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }

            if (route != null)
            {
                BuildRoute(route);
            }

            playerIcon = CreateIcon("Player Icon", new Color(.25f, .9f, 1f, 1f), 10f);
            payloadIcon = CreateIcon("Payload Icon", new Color(1f, .75f, .15f, 1f), 12f);
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
            RefreshRouteColors();
            UpdateIcon(playerIcon, player, mapRect);
            UpdateIcon(payloadIcon, payload, mapRect);
            RefreshEnemyIcons(mapRect);
        }

        public RectTransform GetEnemyIcon(int enemyId)
        {
            return enemyIcons.TryGetValue(enemyId, out var icon) ? icon : null;
        }

        private void BuildRoute(PayloadRoutePlan route)
        {
            var mapRect = panel.rect;
            var cumulativeDistance = 0f;
            for (var index = 1; index < route.Points.Count; index++)
            {
                var start = ProjectWorldInsidePanel(route.Points[index - 1], worldBounds, mapRect);
                var end = ProjectWorldInsidePanel(route.Points[index], worldBounds, mapRect);
                var segment = CreateIcon($"Route Segment {index}", new Color(.2f, .75f, .9f, .75f), 1f);
                var delta = end - start;
                segment.anchoredPosition = (start + end) * .5f;
                segment.sizeDelta = new Vector2(delta.magnitude, 3f);
                segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                routeSegments.Add(segment);
                cumulativeDistance += Vector3.Distance(route.Points[index - 1], route.Points[index]);
                routeSegmentEndDistances.Add(cumulativeDistance);
            }

            for (var index = 0; index < route.CheckpointDistances.Count; index++)
            {
                var marker = CreateIcon($"Checkpoint {index + 1}", new Color(1f, .9f, .25f, 1f), 7f);
                marker.anchoredPosition = ProjectWorldInsidePanel(route.EvaluateDistance(route.CheckpointDistances[index]), worldBounds, mapRect);
                checkpointMarkers.Add(marker);
            }

            var destination = CreateIcon("Destination", new Color(1f, .35f, .2f, 1f), 9f);
            destination.anchoredPosition = ProjectWorldInsidePanel(route.Destination, worldBounds, mapRect);
            checkpointMarkers.Add(destination);
        }

        private void RefreshRouteColors()
        {
            if (routePlan == null)
            {
                return;
            }

            var snapshot = mission != null ? mission.Snapshot : default;
            var routeDistance = mission != null ? snapshot.RouteDistance : 0f;
            var nextCheckpointIndex = mission != null ? snapshot.NextCheckpointIndex : 0;
            var currentLegEnd = nextCheckpointIndex < routePlan.CheckpointDistances.Count
                ? routePlan.CheckpointDistances[nextCheckpointIndex]
                : routePlan.TotalLength;

            for (var index = 0; index < routeSegments.Count; index++)
            {
                var image = routeSegments[index] != null ? routeSegments[index].GetComponent<Image>() : null;
                if (image == null)
                {
                    continue;
                }

                var startDistance = index == 0 ? 0f : routeSegmentEndDistances[index - 1];
                var endDistance = routeSegmentEndDistances[index];
                image.color = endDistance <= routeDistance + .001f
                    ? CompletedRouteColor
                    : startDistance < currentLegEnd
                        ? CurrentRouteColor
                        : FutureRouteColor;
            }

            for (var index = 0; index < checkpointMarkers.Count; index++)
            {
                var image = checkpointMarkers[index] != null ? checkpointMarkers[index].GetComponent<Image>() : null;
                if (image == null)
                {
                    continue;
                }

                var completed = index < nextCheckpointIndex ||
                                snapshot.Phase == PayloadMissionPhase.Completed;
                image.color = completed
                    ? CompletedCheckpointColor
                    : index == nextCheckpointIndex
                        ? ActiveCheckpointColor
                        : FutureCheckpointColor;
            }
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

                    icon.anchoredPosition = ProjectWorldInsidePanel(enemy.transform.position, worldBounds, mapRect);
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
                icon.anchoredPosition = ProjectWorldInsidePanel(target.position, worldBounds, mapRect);
            }
        }

        private RectTransform CreateIcon(string viewName, Color color, float size)
        {
            var iconObject = new GameObject(viewName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var icon = iconObject.GetComponent<RectTransform>();
            icon.SetParent(transform, false);
            icon.anchorMin = icon.anchorMax = panel != null ? panel.pivot : new Vector2(.5f, .5f);
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

            DestroyView(playerIcon);
            DestroyView(payloadIcon);
            routeSegments.Clear();
            checkpointMarkers.Clear();
            routeSegmentEndDistances.Clear();
            enemyIcons.Clear();
            playerIcon = null;
            payloadIcon = null;
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

        private static Rect Inset(Rect rect, float padding)
        {
            if (rect.width <= padding * 2f || rect.height <= padding * 2f)
            {
                return new Rect(rect.center, Vector2.zero);
            }

            return Rect.MinMaxRect(
                rect.xMin + padding,
                rect.yMin + padding,
                rect.xMax - padding,
                rect.yMax - padding);
        }
    }
}
