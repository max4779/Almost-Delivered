using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlmostDelivered.Editor.Frontend
{
    [InitializeOnLoad]
    internal static class PrototypePlayModeStart
    {
        static PrototypePlayModeStart()
        {
            if (Application.isBatchMode)
                EditorSceneManager.playModeStartScene = null;
            EditorApplication.delayCall += SetHubAsPlayStart;
        }

        private static void SetHubAsPlayStart()
        {
            if (Application.isBatchMode)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var hub = AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontendSceneBuilder.DepartureHubScenePath);
            if (hub != null)
                EditorSceneManager.playModeStartScene = hub;
        }
    }
}
