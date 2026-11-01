using System.IO;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Editor.CommanderAI
{
    public static class CommanderAIAssetFactory
    {
        public const string Root = "Assets/AlmostDelivered/Configs/CommanderAI";
        [MenuItem("Almost Delivered/Commander AI/Create or update configs")]
        public static void CreateOrUpdateDefaultConfigs()
        {
            Directory.CreateDirectory(Root + "/Tactics");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            foreach (CommanderDifficulty difficulty in System.Enum.GetValues(typeof(CommanderDifficulty)))
            {
                var source = CommanderDifficultyProfile.Create(difficulty);
                source.name = difficulty.ToString();
                Save(source, Root + "/" + difficulty + ".asset");
            }
            foreach (var tactic in InitialTacticDefinitions.Create())
            {
                tactic.Playbook = Save(tactic.Playbook, Root + "/Tactics/" + tactic.TacticId + "-playbook.asset");
                Save(tactic, Root + "/Tactics/" + tactic.TacticId + ".asset");
            }
            Material("Ground", new Color(.12f, .16f, .22f));
            Material("Player", new Color(.3f, .9f, .8f));
            Material("Dummy", new Color(.7f, .8f, .9f));
            Material("Commander", new Color(.9f, .25f, .65f));
            Material("Shield", new Color(.2f, .55f, 1f));
            Material("Ranged", new Color(1f, .7f, .15f));
            Material("Charger", new Color(1f, .25f, .2f));
            Material("Support", new Color(.25f, 1f, .35f));
            Material("Objective", new Color(.7f, .4f, 1f));
            AssetDatabase.SaveAssets();
        }
        private static T Save<T>(T source, string path) where T : Object
        {
            source.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source, path); return source; }
            EditorUtility.CopySerialized(source, existing);
            EditorUtility.SetDirty(existing); Object.DestroyImmediate(source); return existing;
        }
        public static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.name = name; material.color = color; EditorUtility.SetDirty(material); return material;
        }
    }
}
