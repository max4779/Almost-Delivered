using System.IO;
using AlmostDelivered.Combat;
using AlmostDelivered.Presentation;
using AlmostDelivered.Presentation.Editor;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Debugging;
using AlmostDelivered.CommanderAI.Domain;
using AlmostDelivered.CommanderAI.Execution;
using AlmostDelivered.CommanderAI.Perception;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AlmostDelivered.Editor.CommanderAI
{
    public static class CommanderAITestSceneBuilder
    {
        public const string DefaultScenePath = "Assets/AlmostDelivered/Scenes/CommanderAITest.unity";
        [MenuItem("Almost Delivered/Commander AI/Build deterministic arena")]
        public static void BuildDefault() => BuildOrUpdateTestScene(DefaultScenePath);
        public static void BuildFromCommandLine()
        {
            try { BuildDefault(); Debug.Log("COMMANDER_ARENA_BUILT " + DefaultScenePath); EditorApplication.Exit(0); }
            catch (System.Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        public static Scene BuildOrUpdateTestScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.StartsWith("Assets/AlmostDelivered/") ||
                !scenePath.EndsWith(".unity") || scenePath.Contains("..")) throw new System.ArgumentException("Use a scene path under Assets/AlmostDelivered.", nameof(scenePath));
            LowPolyAssetFactory.CreateOrUpdateAll();
            CommanderAIAssetFactory.CreateOrUpdateDefaultConfigs();
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath)); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Commander AI Arena");
            var arena = root.AddComponent<CommanderArena>();
            arena.Squad = root.AddComponent<SquadController>();
            root.AddComponent<CommanderDebugView>(); root.AddComponent<CommanderGizmoDrawer>();
            var ground = Primitive("Navigation Ground", PrimitiveType.Cube, new Vector3(0, -.5f, 0), new Vector3(100, 1, 70), "Ground");
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.NavigationStatic);
            var objective = Primitive("Fixed Objective", PrimitiveType.Cylinder, new Vector3(0, .08f, 0), new Vector3(4, .08f, 4), "Objective");
            Object.DestroyImmediate(objective.GetComponent<Collider>());
            AttachVisual(objective, VisualArchetype.ObjectiveBeacon, 0f);
            var payload = new GameObject("Payload display");
            payload.transform.SetParent(objective.transform, false);
            payload.transform.position = new Vector3(3, 0, 0);
            AttachVisual(payload, VisualArchetype.Payload, 0f);
            arena.Objective = objective.transform;
            // Fixed objective semantics use the same ground-level point as role anchors and observations.
            var marker = new GameObject("Objective tactical point"); marker.transform.position = new Vector3(0, 1, 0);
            marker.transform.SetParent(objective.transform); arena.Objective = marker.transform;
            arena.Players = new ObservableActor[3];
            for (var i = 0; i < 3; i++)
            {
                var player = Primitive(i == 0 ? "Player" : i == 1 ? "Dummy A" : "Dummy B", PrimitiveType.Capsule,
                    new Vector3(i == 0 ? 0 : i == 1 ? 30 : -30, 1, 0), Vector3.one, i == 0 ? "Player" : "Dummy");
                player.layer = 2; player.AddComponent<ActorHealth>(); arena.Players[i] = player.AddComponent<ObservableActor>();
                if (i == 0) { Object.DestroyImmediate(player.GetComponent<Collider>()); player.AddComponent<CharacterController>(); player.AddComponent<ArenaPlayerController>(); }
                AttachVisual(player, i == 0 ? VisualArchetype.Player : VisualArchetype.Dummy, -1f);
            }
            var commander = Primitive("Commander", PrimitiveType.Capsule, new Vector3(0, 1, -20), new Vector3(1.5f, 1.5f, 1.5f), "Commander");
            arena.Commander = commander.AddComponent<ActorHealth>(); arena.Brain = commander.AddComponent<CommanderBrain>();
            AttachVisual(commander, VisualArchetype.Commander, -1f);
            arena.Members = new MonsterOrderExecutor[8];
            for (var i = 0; i < 8; i++)
            {
                var role = (MonsterRole)(i / 2);
                var member = Primitive(role + " " + (i % 2 + 1), PrimitiveType.Capsule, new Vector3(-10.5f + i * 3, 1, -10), Vector3.one, role.ToString());
                var agent = member.AddComponent<NavMeshAgent>(); agent.baseOffset = 1; agent.speed = 4; agent.stoppingDistance = .25f; agent.radius = .4f;
                arena.Members[i] = member.AddComponent<MonsterOrderExecutor>();
                var perception = member.AddComponent<MonsterPerception>();
                perception.SightRadius = 80; perception.FieldOfViewAngle = 360; perception.TargetMask = 1 << 2; perception.ObstructionMask = 1;
                member.AddComponent<AlertSignal>().EnsureCue();
                AttachVisual(member, (VisualArchetype)System.Enum.Parse(typeof(VisualArchetype), role.ToString()), -1f);
            }
            var anchors = new System.Collections.Generic.List<TacticalAnchor>();
            foreach (TacticalAnchorTag tag in System.Enum.GetValues(typeof(TacticalAnchorTag)))
            {
                var count = tag == TacticalAnchorTag.CommanderSafe ? 1 : 2;
                for (var i = 0; i < count; i++)
                {
                    var position = tag == TacticalAnchorTag.ObjectiveFront ? new Vector3(-2 + 4 * i, 1, -3) :
                        tag == TacticalAnchorTag.CoverRear ? new Vector3(-3 + 6 * i, 1, -13) :
                        tag == TacticalAnchorTag.LeftFlank ? new Vector3(-12, 1, -2 + i * 4) :
                        tag == TacticalAnchorTag.RightFlank ? new Vector3(12, 1, -2 + i * 4) :
                        tag == TacticalAnchorTag.SupportRear ? new Vector3(-3 + 6 * i, 1, -18) : new Vector3(0, 1, -22);
                    var anchorObject = new GameObject(tag + " Anchor " + (i + 1)); anchorObject.transform.position = position;
                    var anchor = anchorObject.AddComponent<TacticalAnchor>(); anchor.Tag = tag;
                    anchor.AllowedRoles = tag == TacticalAnchorTag.ObjectiveFront ? new[] { MonsterRole.Shield } :
                        tag == TacticalAnchorTag.CoverRear ? new[] { MonsterRole.Ranged } :
                        tag == TacticalAnchorTag.SupportRear ? new[] { MonsterRole.Support } :
                        tag == TacticalAnchorTag.CommanderSafe ? new[] { MonsterRole.Support } : new[] { MonsterRole.Charger };
                    anchors.Add(anchor);
                    var disc = Primitive(tag + " Marker " + (i + 1), PrimitiveType.Cylinder, new Vector3(position.x, .04f, position.z), new Vector3(1.4f, .04f, 1.4f), "Objective");
                    Object.DestroyImmediate(disc.GetComponent<Collider>());
                }
            }
            arena.Anchors = anchors.ToArray();
            arena.Profiles = new CommanderDifficultyProfile[3];
            for (var i = 0; i < 3; i++) arena.Profiles[i] = AssetDatabase.LoadAssetAtPath<CommanderDifficultyProfile>(CommanderAIAssetFactory.Root + "/" + (CommanderDifficulty)i + ".asset");
            arena.Tactics = new UtilityTactic[3];
            var ids = new[] { InitialTacticIds.ObjectiveBlockade, InitialTacticIds.Pincer, InitialTacticIds.FocusSupport };
            for (var i = 0; i < ids.Length; i++) arena.Tactics[i] = AssetDatabase.LoadAssetAtPath<UtilityTactic>(CommanderAIAssetFactory.Root + "/Tactics/" + ids[i] + ".asset");
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 58, -28); camera.transform.rotation = Quaternion.Euler(65, 0, 0);
            camera.orthographic = true; camera.orthographicSize = 34; camera.backgroundColor = new Color(.05f, .07f, .11f);
            var light = new GameObject("Arena Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            EditorSceneManager.SaveScene(scene, scenePath);
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            EditorSceneManager.SaveScene(scene, scenePath); AssetDatabase.SaveAssets();
            // Reload the persisted NavMeshSettings reference; rebakes can replace its data instance.
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        private static void AttachVisual(GameObject root, VisualArchetype type, float worldYOffset)
        {
            var renderer = root.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            var visual = LowPolyAssetFactory.InstantiateVisual(root, type).transform;
            visual.position = root.transform.position + Vector3.up * worldYOffset;
            var scale = root.transform.lossyScale;
            visual.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        }
        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, string material)
        {
            var item = GameObject.CreatePrimitive(type); item.name = name; item.transform.position = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CommanderAIAssetFactory.Root + "/Materials/" + material + ".mat");
            return item;
        }
    }
}
