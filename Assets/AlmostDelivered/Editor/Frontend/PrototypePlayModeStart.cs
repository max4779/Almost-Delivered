using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlmostDelivered.Editor.Frontend
{
    [InitializeOnLoad]
    internal static class PrototypePlayModeStart
    {
        private const string TestModeKey = "AlmostDelivered.PlayModeTests";

        static PrototypePlayModeStart()
        {
            if (Application.isBatchMode)
                EditorSceneManager.playModeStartScene = null;
            EditorApplication.delayCall += SetHubAsPlayStart;
        }

        private static void SetHubAsPlayStart()
        {
            if (Application.isBatchMode || SessionState.GetBool(TestModeKey, false))
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var hub = AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontendSceneBuilder.DepartureHubScenePath);
            if (hub != null)
                EditorSceneManager.playModeStartScene = hub;
        }

        [MenuItem("Almost Delivered/Tests/Use test runner start scene")]
        private static void EnableTestRunnerStart()
        {
            SessionState.SetBool(TestModeKey, true);
            SetHubAsPlayStart();
        }

        [MenuItem("Almost Delivered/Tests/Restore hub play start")]
        private static void RestoreHubStart()
        {
            SessionState.SetBool(TestModeKey, false);
            SetHubAsPlayStart();
        }
    }
}
