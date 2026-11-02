using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AlmostDelivered.Presentation
{
    /// <summary>Presentation references only; gameplay stays above VisualRoot.</summary>
    public sealed class LowPolyVisual : MonoBehaviour
    {
        [SerializeField] private VisualArchetype archetype;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] private Transform[] posePivots = Array.Empty<Transform>();

        public VisualArchetype Archetype => archetype;
        public Transform VisualRoot => visualRoot;
        public IReadOnlyList<Renderer> Renderers => renderers;
        public IReadOnlyList<Transform> PosePivots => posePivots;

        public void Configure(VisualArchetype archetype, Transform root, Renderer[] sharedRenderers, Transform[] pivots)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (sharedRenderers == null) throw new ArgumentNullException(nameof(sharedRenderers));
            if (pivots == null) throw new ArgumentNullException(nameof(pivots));
            if (sharedRenderers.Length == 0)
                throw new ArgumentException("At least one shared renderer is required.", nameof(sharedRenderers));
            foreach (var renderer in sharedRenderers)
            {
                if (renderer == null || !renderer.transform.IsChildOf(root))
                    throw new ArgumentException("Renderers must belong to VisualRoot.", nameof(sharedRenderers));
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0 || Array.Exists(materials, material => material == null))
                    throw new ArgumentException("Renderers require non-null shared materials.", nameof(sharedRenderers));
            }
            foreach (var pivot in pivots)
                if (pivot == null || !pivot.IsChildOf(root))
                    throw new ArgumentException("Pose pivots must belong to VisualRoot.", nameof(pivots));

            this.archetype = archetype;
            visualRoot = root;
            renderers = (Renderer[])sharedRenderers.Clone();
            posePivots = (Transform[])pivots.Clone();
        }

        public bool IsGameplayFree(out string reason)
        {
            if (visualRoot == null)
            {
                reason = "VisualRoot is required.";
                return false;
            }

            var offenders = new List<string>();
            foreach (var component in visualRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var type = component.GetType();
                var componentNamespace = type.Namespace ?? string.Empty;
                if (component is Collider || component is Rigidbody || component is Joint ||
                    component is Collider2D || component is Rigidbody2D || component is Joint2D ||
                    component is NavMeshAgent || component is NavMeshObstacle ||
                    IsNamespace(componentNamespace, "AlmostDelivered.Combat") ||
                    IsNamespace(componentNamespace, "AlmostDelivered.CommanderAI"))
                    offenders.Add(type.Name + " on " + component.name);
            }
            reason = string.Join("; ", offenders);
            return offenders.Count == 0;
        }

        private static bool IsNamespace(string candidate, string prefix)
        {
            return candidate == prefix || candidate.StartsWith(prefix + ".", StringComparison.Ordinal);
        }
    }
}
