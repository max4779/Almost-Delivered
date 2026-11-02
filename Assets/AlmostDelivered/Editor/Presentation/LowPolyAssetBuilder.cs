using System;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Presentation.Editor
{
    public static class LowPolyAssetBuilder
    {
        [MenuItem("Almost Delivered/Art/Build low-poly visual set")]
        public static void Build() { LowPolyAssetFactory.CreateOrUpdateAll(); }
        public static void BuildCommandLine()
        {
            try { LowPolyAssetFactory.CreateOrUpdateAll(); AssetDatabase.SaveAssets(); Debug.Log("LOW_POLY_ASSETS_BUILT"); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
