using AlmostDelivered.Networking;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AlmostDelivered.Editor.Frontend
{
    public static class NetworkPrototypeAssetBuilder
    {
        private const string PrefabPath = "Assets/AlmostDelivered/Resources/Network/NetworkPlayerAvatar.prefab";

        [MenuItem("Almost Delivered/Build Network Avatar")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory("Assets/AlmostDelivered/Resources/Network");
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatar.name = "Network Player Avatar";
            avatar.transform.localScale = new Vector3(.85f, 1f, .85f);
            Object.DestroyImmediate(avatar.GetComponent<Collider>());
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/AlmostDelivered/Art/Materials/Generated/Player.mat");
            if (material != null) avatar.GetComponent<Renderer>().sharedMaterial = material;
            avatar.AddComponent<NetworkObject>();
            avatar.AddComponent<NetworkTransform>();
            avatar.AddComponent<NetworkPlayerAvatar>();
            PrefabUtility.SaveAsPrefabAsset(avatar, PrefabPath);
            Object.DestroyImmediate(avatar);
            AssetDatabase.SaveAssets();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("NETWORK_AVATAR_BUILT");
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildWindowsSmokeFromCommandLine()
        {
            try
            {
                Build();
                var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "AlmostDeliveredNetworkSmoke", "AlmostDelivered.exe");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/AlmostDelivered/Scenes/Frontend.unity",
                        "Assets/AlmostDelivered/Scenes/DepartureHub.unity",
                        "Assets/AlmostDelivered/Scenes/MissionPrototype.unity"
                    },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new System.InvalidOperationException("Network smoke build failed: " + report.summary.result);
                Debug.Log("NETWORK_SMOKE_BUILD " + output);
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
