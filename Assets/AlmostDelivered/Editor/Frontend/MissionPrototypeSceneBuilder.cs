using System;
using System.Collections.Generic;
using System.IO;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay;
using AlmostDelivered.Gameplay.Payload;
using AlmostDelivered.MapGeneration;
using AlmostDelivered.Presentation;
using AlmostDelivered.Presentation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Editor.Frontend
{
    public static class MissionPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/AlmostDelivered/Scenes/MissionPrototype.unity";
        public const int MissionSeed = 4711;

        private static readonly Rect MissionBounds = new Rect(-28f, -50f, 56f, 100f);
        private const float RouteCorridorWidth = 8f;
        private const float RoadWidth = 5.5f;
        private const string FontPath = "Assets/AlmostDelivered/Art/Fonts/NotoSansCJKkr-Regular.otf";
        private const string MaterialRoot = "Assets/AlmostDelivered/Art/Materials/Frontend";

        [MenuItem("Almost Delivered/Frontend/Build mission prototype")]
        public static void BuildOrUpdate()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            MissionMapAssetFactory.CreateOrUpdateAll();
            LowPolyAssetFactory.CreateOrUpdateAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Mission Prototype");
            var route = PayloadRoutePlanner.Generate(MissionSeed, MissionBounds, 5, RouteCorridorWidth);

            BuildArena(root.transform);
            BuildPayloadRoad(root.transform, route);
            var projectileTemplate = BuildProjectile(root.transform);
            var routeDirection = (route.Points[1] - route.Points[0]).normalized;
            var player = BuildPlayer(root.transform, route.Points[0] + routeDirection * 3f + Vector3.up, projectileTemplate);
            var payload = BuildPayload(root.transform, route.Points[0]);
            var mapGenerator = BuildMapGenerator(root.transform);
            var anchors = BuildSpawnAnchors(root.transform, route);
            var waveSpawner = BuildWaveSpawner(root.transform, player.transform, projectileTemplate, anchors);

            var controllerObject = new GameObject("Payload Mission Controller");
            controllerObject.transform.SetParent(root.transform, false);
            var mission = controllerObject.AddComponent<PayloadMissionController>();
            mission.Configure(
                true,
                MissionSeed,
                MissionBounds,
                player.transform,
                payload.transform,
                mapGenerator,
                waveSpawner,
                .35f,
                8f,
                4.5f,
                5,
                RouteCorridorWidth);

            BuildPayloadEscortRadius(payload.transform, mission);
            BuildSupplyRelays(root.transform, payload.transform, mission, route);

            BuildCamera(player.transform);
            BuildLighting(root.transform);
            BuildHud(player.GetComponent<ActorHealth>(), player.transform, payload.transform, mission, waveSpawner, route);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("MISSION_PROTOTYPE_BUILT " + ScenePath);
        }

        private static void BuildArena(Transform parent)
        {
            var floor = Material("Floor");
            var dark = Material("Dark");
            Cube("Mission Floor", parent, new Vector3(0f, -.5f, 0f), new Vector3(56f, 1f, 100f), floor);
            Cube("North Boundary", parent, new Vector3(0f, 1f, 50f), new Vector3(56f, 2f, .6f), dark);
            Cube("South Boundary", parent, new Vector3(0f, 1f, -50f), new Vector3(56f, 2f, .6f), dark);
            Cube("West Boundary", parent, new Vector3(-28f, 1f, 0f), new Vector3(.6f, 2f, 100f), dark);
            Cube("East Boundary", parent, new Vector3(28f, 1f, 0f), new Vector3(.6f, 2f, 100f), dark);
        }

        private static void BuildPayloadRoad(Transform parent, PayloadRoutePlan route)
        {
            var roadObject = new GameObject("Payload Road", typeof(MeshFilter), typeof(MeshRenderer), typeof(PayloadRoadView));
            roadObject.transform.SetParent(parent, false);
            var earth = AssetDatabase.LoadAssetAtPath<Material>($"{MissionMapAssetFactory.MaterialRoot}/Earth.mat");
            var moss = AssetDatabase.LoadAssetAtPath<Material>($"{MissionMapAssetFactory.MaterialRoot}/Moss.mat");
            var grass = AssetDatabase.LoadAssetAtPath<GameObject>($"{MissionMapAssetFactory.PrefabRoot}/Foliage/GrassPatch.prefab");
            var tallGrass = AssetDatabase.LoadAssetAtPath<GameObject>($"{MissionMapAssetFactory.PrefabRoot}/Foliage/TallGrass.prefab");
            roadObject.GetComponent<PayloadRoadView>().Configure(
                route,
                MissionSeed,
                RoadWidth,
                earth,
                moss,
                new[] { grass, tallGrass });
        }

        private static GameObject BuildProjectile(Transform parent)
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Projectile Template";
            projectile.transform.SetParent(parent, false);
            projectile.transform.localScale = Vector3.one * .28f;
            projectile.GetComponent<Renderer>().sharedMaterial = Material("Cyan");
            projectile.GetComponent<SphereCollider>().isTrigger = true;
            projectile.AddComponent<CombatProjectile>();
            projectile.SetActive(false);
            return projectile;
        }

        private static GameObject BuildPlayer(Transform parent, Vector3 position, GameObject projectileTemplate)
        {
            var player = Actor("Player", parent, position, CombatTeamId.Player, VisualArchetype.Player, true);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var character = player.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = .5f;
            var attack = player.AddComponent<RangedAttackController>();
            attack.Configure(projectileTemplate, Child(player.transform, "Player Muzzle", new Vector3(0f, .65f, .85f)), 28f, .22f, 30f, 2.2f);
            player.AddComponent<MissionPlayerController>().Configure(character, attack, 6.5f);
            return player;
        }

        private static GameObject BuildPayload(Transform parent, Vector3 position)
        {
            var payload = new GameObject("Payload");
            payload.transform.SetParent(parent, false);
            payload.transform.position = position;
            var collider = payload.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, .65f, 0f);
            collider.size = new Vector3(2.2f, 1.4f, 2.7f);
            LowPolyAssetFactory.InstantiateVisual(payload, VisualArchetype.Payload);
            return payload;
        }

        private static void BuildPayloadEscortRadius(Transform payload, PayloadMissionController mission)
        {
            var radiusObject = new GameObject("Payload Escort Radius", typeof(LineRenderer), typeof(PayloadEscortRadiusView));
            radiusObject.transform.SetParent(payload, false);
            radiusObject.GetComponent<PayloadEscortRadiusView>().Configure(mission, 4.5f, Material("Cyan"));
        }

        private static void BuildSupplyRelays(
            Transform parent,
            Transform payload,
            PayloadMissionController mission,
            PayloadRoutePlan route)
        {
            var cargoProgress = BuildCargoProgress(payload);
            var relayTargets = new List<Transform>(4);
            for (var index = 0; index < 3; index++)
            {
                var relay = BuildRelay(
                    parent,
                    $"Supply Relay {index + 1}",
                    route.EvaluateDistance(route.CheckpointDistances[index]),
                    index,
                    false,
                    mission,
                    cargoProgress[index]);
                relayTargets.Add(relay.transform);
            }

            var destination = BuildRelay(
                parent,
                "Final Destination Relay",
                route.Destination,
                3,
                true,
                mission,
                null);
            relayTargets.Add(destination.transform);

            var navigator = new GameObject("Payload Relay Navigator");
            navigator.transform.SetParent(payload, false);
            navigator.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            var antenna = Primitive(
                PrimitiveType.Cube,
                "Antenna",
                navigator.transform,
                new Vector3(0f, 0f, .72f),
                new Vector3(.18f, .12f, 1.4f),
                Material("Amber"));
            antenna.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var antennaTip = Primitive(
                PrimitiveType.Cube,
                "Antenna Tip",
                navigator.transform,
                new Vector3(0f, 0f, 1.55f),
                new Vector3(.42f, .18f, .6f),
                Material("Amber"));
            antennaTip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            navigator.AddComponent<PayloadNavigationIndicator>().Configure(mission, relayTargets);
        }

        private static GameObject BuildRelay(
            Transform parent,
            string name,
            Vector3 position,
            int checkpointIndex,
            bool finalDestination,
            PayloadMissionController mission,
            GameObject collectedCargo)
        {
            var relay = new GameObject(name);
            relay.transform.SetParent(parent, false);
            relay.transform.position = position;

            var signalMaterial = finalDestination ? Material("Player") : Material("Cyan");
            Primitive(PrimitiveType.Cylinder, "Relay Base", relay.transform, new Vector3(0f, .12f, 0f), new Vector3(2.5f, .12f, 2.5f), Material("Dark"));
            Primitive(PrimitiveType.Cylinder, "Relay Mast", relay.transform, new Vector3(0f, 1.55f, 0f), new Vector3(.34f, 1.5f, .34f), signalMaterial);
            var ring = Primitive(PrimitiveType.Cylinder, "Pulse Ring", relay.transform, new Vector3(0f, .28f, 0f), new Vector3(2.2f, .04f, 2.2f), Material("Amber"));
            var beam = Primitive(PrimitiveType.Cube, "Signal Beam", relay.transform, new Vector3(0f, 4.2f, 0f), new Vector3(.16f, 7.2f, .16f), signalMaterial);

            var labelObject = new GameObject("Relay Label", typeof(TextMesh));
            labelObject.transform.SetParent(relay.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 3.15f, -.45f);
            labelObject.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            var label = labelObject.GetComponent<TextMesh>();
            label.text = finalDestination ? "DESTINATION" : $"SUPPLY {checkpointIndex + 1}";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = .08f;
            label.color = finalDestination ? new Color(.35f, 1f, .72f) : new Color(.4f, .95f, 1f);

            GameObject cache = null;
            if (!finalDestination)
            {
                cache = new GameObject("Supply Cache");
                cache.transform.SetParent(relay.transform, false);
                cache.transform.localPosition = new Vector3(1.8f, 0f, 0f);
                Primitive(PrimitiveType.Cube, "Cargo Crate A", cache.transform, new Vector3(0f, .48f, 0f), new Vector3(1.15f, .95f, 1.15f), Material("Amber"));
                Primitive(PrimitiveType.Cube, "Cargo Crate B", cache.transform, new Vector3(.75f, .34f, .35f), new Vector3(.65f, .68f, .72f), Material("Dark"));
                Primitive(PrimitiveType.Cylinder, "Fuel Cell", cache.transform, new Vector3(-.72f, .52f, .25f), new Vector3(.34f, .52f, .34f), Material("Cyan"));
            }
            else
            {
                Primitive(PrimitiveType.Cylinder, "Destination Platform", relay.transform, new Vector3(0f, .18f, 0f), new Vector3(3.6f, .08f, 3.6f), Material("Player"));
            }

            relay.AddComponent<PayloadRelayBeaconView>().Configure(
                mission,
                checkpointIndex,
                finalDestination,
                beam,
                ring,
                cache,
                collectedCargo);
            return relay;
        }

        private static GameObject[] BuildCargoProgress(Transform payload)
        {
            var cargo = new GameObject[3];
            for (var index = 0; index < cargo.Length; index++)
            {
                cargo[index] = Primitive(
                    index == 2 ? PrimitiveType.Cylinder : PrimitiveType.Cube,
                    $"Collected Cargo {index + 1}",
                    payload,
                    new Vector3((index - 1) * .58f, 1.55f, -.15f),
                    index == 2 ? new Vector3(.28f, .46f, .28f) : new Vector3(.5f, .55f, .58f),
                    index == 1 ? Material("Cyan") : Material("Amber"));
                cargo[index].SetActive(false);
            }

            return cargo;
        }

        private static MissionMapGenerator BuildMapGenerator(Transform parent)
        {
            var mapObject = new GameObject("Mission Map Generator");
            mapObject.transform.SetParent(parent, false);
            var generator = mapObject.AddComponent<MissionMapGenerator>();
            generator.Configure(
                AssetDatabase.LoadAssetAtPath<MissionMapTheme>(MissionMapAssetFactory.ThemePath),
                new Vector2(28f, 50f), 40, 6, 28, 42,
                new[]
                {
                    new MissionMapProtectedZone(new Vector2(0f, -46f), new Vector2(8f, 8f)),
                    new MissionMapProtectedZone(new Vector2(0f, 46f), new Vector2(8f, 8f))
                },
                false,
                6000);
            return generator;
        }

        private static Transform[] BuildSpawnAnchors(Transform parent, PayloadRoutePlan route)
        {
            var anchors = new Transform[4];
            for (var index = 0; index < 3; index++)
            {
                var side = index % 2 == 0 ? 1f : -1f;
                anchors[index] = Child(
                    parent,
                    $"Wave Spawn {index + 1}",
                    SpawnAnchorPosition(route, route.CheckpointDistances[index], side));
            }

            var finalDistance = Mathf.Max(0f, route.TotalLength - 10f);
            anchors[3] = Child(parent, "Final Wave Spawn", SpawnAnchorPosition(route, finalDistance, -1f));
            return anchors;
        }

        private static Vector3 SpawnAnchorPosition(PayloadRoutePlan route, float distance, float side)
        {
            var center = route.EvaluateDistance(distance);
            var before = route.EvaluateDistance(Mathf.Max(0f, distance - 1f));
            var after = route.EvaluateDistance(Mathf.Min(route.TotalLength, distance + 1f));
            var tangent = Vector3.ProjectOnPlane(after - before, Vector3.up).normalized;
            if (tangent.sqrMagnitude < .0001f)
                tangent = Vector3.forward;
            var lateral = new Vector3(-tangent.z, 0f, tangent.x);
            return center + lateral * (6.5f * Mathf.Sign(side));
        }

        private static PayloadWaveSpawner BuildWaveSpawner(Transform parent, Transform player, GameObject projectile, Transform[] anchors)
        {
            var wavesObject = new GameObject("Payload Wave Spawner");
            wavesObject.transform.SetParent(parent, false);
            var waves = wavesObject.AddComponent<PayloadWaveSpawner>();
            waves.Configure(
                player,
                projectile,
                LowPolyAssetFactory.LoadPrefab(VisualArchetype.Charger),
                LowPolyAssetFactory.LoadPrefab(VisualArchetype.Ranged),
                LowPolyAssetFactory.LoadPrefab(VisualArchetype.Shield),
                LowPolyAssetFactory.LoadPrefab(VisualArchetype.Commander),
                LowPolyAssetFactory.LoadPrefab(VisualArchetype.Charger),
                anchors,
                MissionSeed,
                18);
            return waves;
        }

        private static void BuildCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(QuarterViewCamera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .05f);
            var follow = cameraObject.GetComponent<QuarterViewCamera>();
            follow.Target = target;
            follow.Offset = new Vector3(0f, 16f, -16f);
            follow.FocusOffset = new Vector3(0f, 0f, 3f);
            follow.SnapToTarget();
        }

        private static void BuildLighting(Transform parent)
        {
            var lightObject = new GameObject("Mission Light", typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            RenderSettings.ambientLight = new Color(.3f, .34f, .4f);
        }

        private static void BuildHud(ActorHealth health, Transform player, Transform payload, PayloadMissionController mission, PayloadWaveSpawner waves, PayloadRoutePlan route)
        {
            var canvasObject = new GameObject("Mission HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MissionHud));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var controls = HudText("Controls", canvasObject.transform, new Vector2(24f, -22f), new Vector2(950f, 54f), 26, TextAnchor.UpperLeft);
            var playerHealth = HudText("Player Health", canvasObject.transform, new Vector2(24f, -82f), new Vector2(420f, 48f), 28, TextAnchor.UpperLeft);
            var targetCount = HudText("Target Count", canvasObject.transform, new Vector2(24f, -132f), new Vector2(420f, 42f), 24, TextAnchor.UpperLeft);
            var phase = HudText("Mission Phase", canvasObject.transform, new Vector2(0f, -22f), new Vector2(620f, 52f), 30, TextAnchor.UpperCenter, false, true);
            var progress = HudText("Payload Progress", canvasObject.transform, new Vector2(0f, -72f), new Vector2(420f, 42f), 26, TextAnchor.UpperCenter, false, true);
            var checkpoint = HudText("Checkpoint", canvasObject.transform, new Vector2(24f, -190f), new Vector2(420f, 42f), 24, TextAnchor.UpperLeft);
            var escort = HudText("Escort State", canvasObject.transform, new Vector2(24f, -234f), new Vector2(420f, 42f), 24, TextAnchor.UpperLeft);
            var elapsed = HudText("Elapsed Time", canvasObject.transform, new Vector2(24f, -278f), new Vector2(420f, 42f), 24, TextAnchor.UpperLeft);

            var minimapObject = new GameObject("Mission Minimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(MissionMinimap));
            minimapObject.transform.SetParent(canvasObject.transform, false);
            var minimapRect = minimapObject.GetComponent<RectTransform>();
            minimapRect.anchorMin = minimapRect.anchorMax = minimapRect.pivot = Vector2.one;
            minimapRect.anchoredPosition = new Vector2(-24f, -24f);
            minimapRect.sizeDelta = new Vector2(260f, 210f);
            minimapObject.GetComponent<Image>().color = new Color(.02f, .04f, .06f, .78f);
            minimapObject.GetComponent<MissionMinimap>().Configure(MissionBounds, route, player, payload, mission, waves);

            var hud = canvasObject.GetComponent<MissionHud>();
            hud.BindViews(controls, playerHealth, targetCount);
            hud.Configure(health, Array.Empty<PrototypeBossController>());
            hud.ConfigurePayload(mission, phase, progress, checkpoint, escort, elapsed);
        }

        private static GameObject Actor(string name, Transform parent, Vector3 position, CombatTeamId team, VisualArchetype archetype, bool lowerVisual)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(parent, false);
            actor.transform.position = position;
            var collider = actor.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = .5f;
            actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = team;
            var visual = LowPolyAssetFactory.InstantiateVisual(actor, archetype);
            visual.transform.localPosition = lowerVisual ? Vector3.down : Vector3.zero;
            return actor;
        }

        private static Text HudText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, bool right = false, bool center = false)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            var anchor = center ? new Vector2(.5f, 1f) : right ? Vector2.one : new Vector2(0f, 1f);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.GetComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static GameObject Primitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = localScale;
            value.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
            return value;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
    }
}
