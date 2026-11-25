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
        [SerializeField] private List<RectTransform> sideMissionIcons = new List<RectTransform>();
        [SerializeField] private List<RectTransform> fuelIcons = new List<RectTransform>();
        private readonly List<float> routeSegmentEndDistances = new List<float>();
        private readonly Dictionary<int, RectTransform> enemyIcons = new Dictionary<int, RectTransform>();
        private readonly List<int> staleEnemyIds = new List<int>();
        private readonly Dictionary<int, RectTransform> scoutIcons = new Dictionary<int, RectTransform>();
        private readonly List<int> staleScoutIds = new List<int>();

        [SerializeField] private Rect worldBounds;
        [SerializeField] private Transform player;
        [SerializeField] private Transform payload;
        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private PayloadWaveSpawner waves;
        [SerializeField] private SideMissionDirector sideMissions;
        [SerializeField] private FuelDepotDirector fuelDepots;
        [SerializeField] private MissionCommanderDirector scoutDirector;
        [SerializeField] private bool showScoutsForDevelopment = true;
        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform playerIcon;
        [SerializeField] private RectTransform payloadIcon;
        private RectTransform evacuationIcon;
        private RectTransform evacuationArrow;
        private Text mapHint;
        private PayloadRoutePlan routePlan;
        private Vector2 compactAnchor;
        private Vector2 compactPosition;
        private Vector2 compactSize;
        private int compactSiblingIndex;
        private Color compactBackgroundColor;
        private bool layoutSaved;
        private bool fullMapOpen;
        private const float LocalMapWidth = 100f;

        private static readonly Color CurrentRouteColor = new Color(.2f, .85f, 1f, .95f);
        private static readonly Color FutureRouteColor = new Color(.15f, .38f, .48f, .28f);
        private static readonly Color CompletedRouteColor = new Color(.2f, .65f, .4f, .6f);
        private static readonly Color ActiveCheckpointColor = new Color(1f, .9f, .25f, 1f);
        private static readonly Color FutureCheckpointColor = new Color(.22f, .38f, .46f, .45f);
        private static readonly Color CompletedCheckpointColor = new Color(.2f, .8f, .35f, .85f);

        public int RouteSegmentCount => routeSegments.Count;
        public int CheckpointMarkerCount => checkpointMarkers.Count;
        public int EnemyIconCount => enemyIcons.Count;
        public int ScoutIconCount => scoutIcons.Count;
        public bool ShowScoutsForDevelopment => showScoutsForDevelopment;
        public RectTransform PlayerIcon => playerIcon;
        public RectTransform PayloadIcon => payloadIcon;
        public bool IsFullMapOpen => fullMapOpen;
        public Rect CurrentViewBounds => GetViewBounds();
        public RectTransform EvacuationArrow => evacuationArrow;

        public void SetFullMapOpen(bool open)
        {
            if (panel == null || fullMapOpen == open) return;
            SaveCompactLayout();
            fullMapOpen = open;
            if (open)
            {
                var canvasRect = panel.parent as RectTransform;
                var availableHeight = canvasRect != null && canvasRect.rect.height > 0f
                    ? canvasRect.rect.height * .78f : 780f;
                var availableWidth = canvasRect != null && canvasRect.rect.width > 0f
                    ? canvasRect.rect.width * .78f : 780f;
                var height = Mathf.Min(availableHeight, availableWidth * worldBounds.height /
                    Mathf.Max(1f, worldBounds.width));
                var width = height * worldBounds.width / Mathf.Max(1f, worldBounds.height);
                panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
                panel.sizeDelta = new Vector2(width, height);
                panel.anchoredPosition = new Vector2(width * .5f, height * .5f);
                panel.SetAsLastSibling();
            }
            else
            {
                panel.anchorMin = panel.anchorMax = compactAnchor;
                panel.sizeDelta = compactSize;
                panel.anchoredPosition = compactPosition;
                panel.SetSiblingIndex(compactSiblingIndex);
            }
            var background = panel.GetComponent<Image>();
            if (background != null)
            {
                background.color = open
                    ? new Color(compactBackgroundColor.r, compactBackgroundColor.g,
                        compactBackgroundColor.b, .97f)
                    : compactBackgroundColor;
            }
            if (mapHint != null) mapHint.text = open ? "M CLOSE" : "M MAP";
            Refresh();
        }

        public void BindSideMissions(SideMissionDirector director)
        {
            foreach (var icon in sideMissionIcons) DestroyView(icon);
            sideMissionIcons.Clear();
            sideMissions = director;
            if (panel == null || director == null) return;
            for (var index = 0; index < director.Count; index++)
            {
                var commander = director.PlacementAt(index).kind == SideMissionKind.Commander;
                var icon = CreateIcon($"Side Mission {index + 1}",
                    commander ? new Color(1f, .45f, .18f) : new Color(.3f, 1f, .85f), 11f);
                sideMissionIcons.Add(icon);
            }
            Refresh();
        }

        public void BindFuelDepots(FuelDepotDirector director)
        {
            foreach (var icon in fuelIcons) DestroyView(icon);
            fuelIcons.Clear();
            fuelDepots = director;
            if (panel == null || director == null) return;
            for (var index = 0; index < director.Count; index++)
                fuelIcons.Add(CreateIcon($"Fuel Depot {index + 1}", new Color(.4f, 1f, .5f), 6f));
            Refresh();
        }

        public void BindScouts(MissionCommanderDirector director)
        {
            scoutDirector = director;
            Refresh();
        }

        public void SetScoutDebugVisibility(bool visible)
        {
            showScoutsForDevelopment = visible;
            Refresh();
        }

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
            SaveCompactLayout();

            if (route != null)
            {
                BuildRoute(route);
            }

            playerIcon = CreateIcon("Player Icon", new Color(.25f, .9f, 1f, 1f), 10f);
            payloadIcon = CreateIcon("Payload Icon", new Color(1f, .75f, .15f, 1f), 12f);
            evacuationIcon = CreateIcon("Evacuation Site", new Color(1f, .25f, .72f, 1f), 15f);
            EnsureMapHint();
            Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) SetFullMapOpen(!fullMapOpen);
            Refresh();
        }

        public void Refresh()
        {
            if (panel == null)
            {
                return;
            }
            EnsureRoutePlan();
            EnsureMapHint();

            // Existing generated scenes may predate the extraction marker. The field is
            // runtime-only, so recover an existing child or create one when needed.
            if (evacuationIcon == null)
                evacuationIcon = transform.Find("Evacuation Site") as RectTransform ??
                    CreateIcon("Evacuation Site", new Color(1f, .25f, .72f, 1f), 15f);

            var mapRect = panel.rect;
            var viewBounds = GetViewBounds();
            RefreshRouteVisibility(mapRect, viewBounds);
            RefreshRouteColors();
            UpdateIcon(playerIcon, player, mapRect, viewBounds);
            UpdateIcon(payloadIcon, payload, mapRect, viewBounds);
            RefreshPayloadIcon();
            RefreshEvacuationIcon(mapRect, viewBounds);
            RefreshEnemyIcons(mapRect, viewBounds);
            RefreshScoutIcons(mapRect, viewBounds);
            for (var index = 0; index < sideMissionIcons.Count; index++)
            {
                var icon = sideMissionIcons[index];
                if (icon == null) continue;
                var placement = sideMissions.PlacementAt(index);
                var state = sideMissions.StateAt(index);
                var position = state == SideMissionState.Carrying && player != null ? player.position : placement.position;
                icon.gameObject.SetActive((state == SideMissionState.Available || state == SideMissionState.Carrying) &&
                    IsInside(position, viewBounds));
                icon.anchoredPosition = ProjectWorldInsidePanel(position, viewBounds, mapRect);
            }
            for (var index = 0; index < fuelIcons.Count; index++)
            {
                var icon = fuelIcons[index];
                if (icon == null || fuelDepots == null) continue;
                var position = fuelDepots.CarryingIndex == index && player != null
                    ? player.position : fuelDepots.PlacementAt(index).position;
                icon.gameObject.SetActive(!fuelDepots.IsCollected(index) && IsInside(position, viewBounds));
                icon.anchoredPosition = ProjectWorldInsidePanel(position, viewBounds, mapRect);
            }
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

        private void EnsureRoutePlan()
        {
            if (routePlan == null && mission != null) routePlan = mission.RoutePlan;
            if (routePlan == null || routeSegmentEndDistances.Count == routeSegments.Count) return;
            routeSegmentEndDistances.Clear();
            var distance = 0f;
            for (var index = 1; index < routePlan.Points.Count; index++)
            {
                distance += Vector3.Distance(routePlan.Points[index - 1], routePlan.Points[index]);
                routeSegmentEndDistances.Add(distance);
            }
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

        private void RefreshRouteVisibility(Rect mapRect, Rect viewBounds)
        {
            var revealRoute = routePlan != null && (mission == null ||
                              (mission.Snapshot.Phase != PayloadMissionPhase.EvacuationTravel &&
                               mission.Snapshot.Phase != PayloadMissionPhase.EvacuationDefense &&
                               mission.Snapshot.Phase != PayloadMissionPhase.Escaped &&
                               mission.Snapshot.Phase != PayloadMissionPhase.Deploying &&
                               mission.Snapshot.Phase != PayloadMissionPhase.Recovering &&
                               mission.Snapshot.Phase != PayloadMissionPhase.Activating));
            if (routePlan == null) return;
            for (var index = 0; index < routeSegments.Count; index++)
            {
                var segment = routeSegments[index];
                if (segment == null) continue;
                var start = routePlan.Points[index];
                var end = routePlan.Points[index + 1];
                var visible = revealRoute && ClipSegment(viewBounds, ref start, ref end);
                if (visible)
                {
                    var from = ProjectWorldInsidePanel(start, viewBounds, mapRect);
                    var to = ProjectWorldInsidePanel(end, viewBounds, mapRect);
                    var delta = to - from;
                    segment.anchoredPosition = (from + to) * .5f;
                    segment.sizeDelta = new Vector2(delta.magnitude, 3f);
                    segment.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
                segment.gameObject.SetActive(visible);
            }

            for (var index = 0; index < checkpointMarkers.Count; index++)
            {
                var marker = checkpointMarkers[index];
                if (marker == null) continue;
                var position = index < routePlan.CheckpointDistances.Count
                    ? routePlan.EvaluateDistance(routePlan.CheckpointDistances[index])
                    : routePlan.Destination;
                var visible = revealRoute && IsInside(position, viewBounds);
                if (visible) marker.anchoredPosition = ProjectWorldInsidePanel(position, viewBounds, mapRect);
                marker.gameObject.SetActive(visible);
            }
        }

        private void RefreshEvacuationIcon(Rect mapRect, Rect viewBounds)
        {
            if (evacuationIcon == null) return;
            var phase = mission != null ? mission.Snapshot.Phase : PayloadMissionPhase.Deploying;
            var visible = phase == PayloadMissionPhase.EvacuationTravel ||
                          phase == PayloadMissionPhase.EvacuationDefense;
            evacuationIcon.gameObject.SetActive(visible);
            if (!visible) return;

            EnsureEvacuationArrow();
            var target = mission.EvacuationTarget;
            var inView = IsInside(target, viewBounds);
            var image = evacuationIcon.GetComponent<Image>();
            image.color = inView ? new Color(1f, .25f, .72f, 1f) : Color.clear;
            evacuationArrow.gameObject.SetActive(!inView);
            if (inView)
            {
                evacuationIcon.anchoredPosition = ProjectWorldInsidePanel(target, viewBounds, mapRect);
                return;
            }

            var origin = player != null ? player.position : Vector3.zero;
            var direction = new Vector2(target.x - origin.x, target.z - origin.z);
            if (direction.sqrMagnitude < .001f) direction = Vector2.up;
            var edge = Inset(mapRect, ContentPadding + 12f);
            var center = ProjectWorldInsidePanel(origin, viewBounds, mapRect);
            center.x = Mathf.Clamp(center.x, edge.xMin, edge.xMax);
            center.y = Mathf.Clamp(center.y, edge.yMin, edge.yMax);
            var maxX = direction.x > 0f ? (edge.xMax - center.x) / direction.x :
                direction.x < 0f ? (edge.xMin - center.x) / direction.x : float.PositiveInfinity;
            var maxY = direction.y > 0f ? (edge.yMax - center.y) / direction.y :
                direction.y < 0f ? (edge.yMin - center.y) / direction.y : float.PositiveInfinity;
            var distance = Mathf.Max(0f, Mathf.Min(maxX, maxY));
            evacuationIcon.anchoredPosition = center + direction * distance;
            evacuationArrow.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }

        private void EnsureEvacuationArrow()
        {
            if (evacuationArrow != null) return;
            evacuationArrow = evacuationIcon.Find("Direction Arrow") as RectTransform;
            if (evacuationArrow != null) return;
            var arrowObject = new GameObject("Direction Arrow", typeof(RectTransform));
            evacuationArrow = arrowObject.GetComponent<RectTransform>();
            evacuationArrow.SetParent(evacuationIcon, false);
            evacuationArrow.anchorMin = evacuationArrow.anchorMax = evacuationArrow.pivot =
                new Vector2(.5f, .5f);
            evacuationArrow.sizeDelta = new Vector2(20f, 20f);
            CreateArrowBar("Arrow Stem", new Vector2(0f, -2f), new Vector2(3f, 14f), 0f);
            CreateArrowBar("Arrow Left", new Vector2(-4f, 5f), new Vector2(3f, 11f), -45f);
            CreateArrowBar("Arrow Right", new Vector2(4f, 5f), new Vector2(3f, 11f), 45f);
        }

        private void CreateArrowBar(string label, Vector2 position, Vector2 size, float angle)
        {
            var bar = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = bar.GetComponent<RectTransform>();
            rect.SetParent(evacuationArrow, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = bar.GetComponent<Image>();
            image.color = new Color(1f, .3f, .72f, 1f);
            image.raycastTarget = false;
        }

        private void EnsureMapHint()
        {
            if (mapHint != null) return;
            var existing = transform.Find("Map Toggle Hint");
            if (existing != null) mapHint = existing.GetComponent<Text>();
            if (mapHint != null) return;
            var hint = new GameObject("Map Toggle Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = hint.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-14f, -9f);
            rect.sizeDelta = new Vector2(100f, 20f);
            mapHint = hint.GetComponent<Text>();
            mapHint.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            mapHint.fontSize = 14;
            mapHint.alignment = TextAnchor.UpperRight;
            mapHint.color = new Color(.8f, .9f, 1f, .9f);
            mapHint.raycastTarget = false;
            mapHint.text = fullMapOpen ? "M CLOSE" : "M MAP";
        }

        private void SaveCompactLayout()
        {
            if (layoutSaved || panel == null) return;
            compactAnchor = panel.anchorMin;
            compactPosition = panel.anchoredPosition;
            compactSize = panel.sizeDelta;
            compactSiblingIndex = panel.GetSiblingIndex();
            var background = panel.GetComponent<Image>();
            if (background != null) compactBackgroundColor = background.color;
            layoutSaved = true;
        }

        private Rect GetViewBounds()
        {
            if (fullMapOpen || player == null || panel == null ||
                !IsFinitePositive(worldBounds.width) || !IsFinitePositive(worldBounds.height))
                return worldBounds;
            var width = Mathf.Min(LocalMapWidth, worldBounds.width);
            var height = Mathf.Min(worldBounds.height,
                width * Mathf.Max(1f, panel.rect.height) / Mathf.Max(1f, panel.rect.width));
            var centerX = Mathf.Clamp(player.position.x, worldBounds.xMin + width * .5f,
                worldBounds.xMax - width * .5f);
            var centerY = Mathf.Clamp(player.position.z, worldBounds.yMin + height * .5f,
                worldBounds.yMax - height * .5f);
            return new Rect(centerX - width * .5f, centerY - height * .5f, width, height);
        }

        private static bool IsInside(Vector3 position, Rect bounds)
        {
            return bounds.Contains(new Vector2(position.x, position.z));
        }

        private static bool ClipSegment(Rect bounds, ref Vector3 start, ref Vector3 end)
        {
            var delta = end - start;
            var first = 0f;
            var last = 1f;
            if (!ClipAxis(-delta.x, start.x - bounds.xMin, ref first, ref last) ||
                !ClipAxis(delta.x, bounds.xMax - start.x, ref first, ref last) ||
                !ClipAxis(-delta.z, start.z - bounds.yMin, ref first, ref last) ||
                !ClipAxis(delta.z, bounds.yMax - start.z, ref first, ref last)) return false;
            var original = start;
            start = original + delta * first;
            end = original + delta * last;
            return true;
        }

        private static bool ClipAxis(float direction, float offset, ref float first, ref float last)
        {
            if (Mathf.Abs(direction) < .0001f) return offset >= 0f;
            var boundary = offset / direction;
            if (direction < 0f) first = Mathf.Max(first, boundary);
            else last = Mathf.Min(last, boundary);
            return first <= last;
        }

        private void RefreshPayloadIcon()
        {
            if (payloadIcon == null)
            {
                return;
            }

            var recovering = mission != null &&
                             (mission.Snapshot.Phase == PayloadMissionPhase.Deploying ||
                              mission.Snapshot.Phase == PayloadMissionPhase.Recovering ||
                              mission.Snapshot.Phase == PayloadMissionPhase.Activating);
            payloadIcon.sizeDelta = Vector2.one * (recovering ? 16f : 12f);
            var image = payloadIcon.GetComponent<Image>();
            if (image != null)
            {
                image.color = recovering
                    ? new Color(1f, .28f, .12f, 1f)
                    : new Color(1f, .75f, .15f, 1f);
            }
        }

        private void RefreshEnemyIcons(Rect mapRect, Rect viewBounds)
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

                    icon.gameObject.SetActive(IsInside(enemy.transform.position, viewBounds));
                    icon.anchoredPosition = ProjectWorldInsidePanel(enemy.transform.position, viewBounds, mapRect);
                }
            }

            foreach (var enemyId in staleEnemyIds)
            {
                var icon = enemyIcons[enemyId];
                enemyIcons.Remove(enemyId);
                DestroyView(icon);
            }
        }

        private void RefreshScoutIcons(Rect mapRect, Rect viewBounds)
        {
            staleScoutIds.Clear();
            foreach (var pair in scoutIcons) staleScoutIds.Add(pair.Key);
            if (showScoutsForDevelopment && scoutDirector != null)
            {
                foreach (var scout in scoutDirector.Scouts)
                {
                    if (scout == null || scout.IsDefeated) continue;
                    staleScoutIds.Remove(scout.ReporterId);
                    if (!scoutIcons.TryGetValue(scout.ReporterId, out var icon))
                    {
                        icon = CreateIcon($"Scout {scout.ReporterId}",
                            new Color(.92f, .38f, 1f, 1f), 9f);
                        scoutIcons.Add(scout.ReporterId, icon);
                    }
                    icon.gameObject.SetActive(IsInside(scout.transform.position, viewBounds));
                    icon.anchoredPosition = ProjectWorldInsidePanel(
                        scout.transform.position, viewBounds, mapRect);
                }
            }
            foreach (var reporterId in staleScoutIds)
            {
                var icon = scoutIcons[reporterId];
                scoutIcons.Remove(reporterId);
                DestroyView(icon);
            }
        }

        private void UpdateIcon(RectTransform icon, Transform target, Rect mapRect, Rect viewBounds)
        {
            if (icon == null)
            {
                return;
            }

            icon.gameObject.SetActive(target != null);
            if (target != null)
            {
                icon.anchoredPosition = ProjectWorldInsidePanel(target.position, viewBounds, mapRect);
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
            foreach (var icon in scoutIcons.Values)
                DestroyView(icon);

            foreach (var icon in sideMissionIcons)
                DestroyView(icon);
            foreach (var icon in fuelIcons)
                DestroyView(icon);

            DestroyView(playerIcon);
            DestroyView(payloadIcon);
            DestroyView(evacuationIcon);
            DestroyView(mapHint != null ? mapHint.rectTransform : null);
            routeSegments.Clear();
            checkpointMarkers.Clear();
            routeSegmentEndDistances.Clear();
            enemyIcons.Clear();
            scoutIcons.Clear();
            sideMissionIcons.Clear();
            fuelIcons.Clear();
            playerIcon = null;
            payloadIcon = null;
            evacuationIcon = null;
            evacuationArrow = null;
            mapHint = null;
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
