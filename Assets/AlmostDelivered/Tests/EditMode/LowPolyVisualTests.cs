using System;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Perception;
using AlmostDelivered.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class LowPolyVisualTests
    {
        private GameObject actor;
        private Transform visualRoot;
        private MeshRenderer renderer;
        private Material material;
        private Mesh clothMesh;
        private LowPolyVisual visual;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Gameplay root");
            visualRoot = Child(actor.transform, "VisualRoot");
            renderer = Child(visualRoot, "Body").gameObject.AddComponent<MeshRenderer>();
            material = new Material(Shader.Find("Standard"));
            renderer.sharedMaterial = material;
            visual = visualRoot.gameObject.AddComponent<LowPolyVisual>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(material);
            if (clothMesh != null) UnityEngine.Object.DestroyImmediate(clothMesh);
        }

        [Test]
        public void Configure_RequiresVisualRootAndSharedRenderers()
        {
            var renderers = new Renderer[] { renderer };
            var pivots = new[] { renderer.transform };
            Assert.Throws<ArgumentNullException>(() => visual.Configure(VisualArchetype.Shield, null, renderers, pivots));
            Assert.Throws<ArgumentNullException>(() => visual.Configure(VisualArchetype.Shield, visualRoot, null, pivots));
            Assert.Throws<ArgumentException>(() => visual.Configure(VisualArchetype.Shield, visualRoot, Array.Empty<Renderer>(), pivots));
            Assert.Throws<ArgumentException>(() => visual.Configure(VisualArchetype.Shield, visualRoot, new Renderer[] { null }, pivots));
            renderer.sharedMaterial = null;
            Assert.Throws<ArgumentException>(() => visual.Configure(VisualArchetype.Shield, visualRoot, renderers, pivots));
            renderer.sharedMaterial = material;
            visual.Configure(VisualArchetype.Shield, visualRoot, renderers, pivots);
            Assert.That(visual.Archetype, Is.EqualTo(VisualArchetype.Shield));
            Assert.That(visual.VisualRoot, Is.SameAs(visualRoot));
            Assert.That(visual.Renderers, Is.EqualTo(renderers));
            Assert.That(visual.PosePivots, Is.EqualTo(pivots));
            Assert.That(visual.IsGameplayFree(out var reason), Is.True, reason);
        }

        [TestCase(typeof(BoxCollider), false)]
        [TestCase(typeof(CharacterController), true)]
        [TestCase(typeof(NavMeshAgent), true)]
        [TestCase(typeof(Rigidbody), true)]
        [TestCase(typeof(BoxCollider2D), true)]
        [TestCase(typeof(Rigidbody2D), true)]
        [TestCase(typeof(ArticulationBody), true)]
        [TestCase(typeof(Cloth), true)]
        public void IsGameplayFree_RejectsColliderOrNavMeshAgentAnywhereBelowVisualRoot(Type type, bool inactive)
        {
            AssertRejected(type, inactive);
        }

        [TestCase(typeof(ActorHealth))]
        [TestCase(typeof(StunState))]
        [TestCase(typeof(ObservableActor))]
        [TestCase(typeof(MonsterPerception))]
        [TestCase(typeof(CommanderBrain))]
        [TestCase(typeof(SquadController))]
        public void IsGameplayFree_RejectsHealthPerceptionOrCommanderAIComponentsBelowVisualRoot(Type type)
        {
            AssertRejected(type, true);
        }

        [Test]
        public void SharedMaterials_DoesNotInstantiateRendererMaterials()
        {
            var before = renderer.sharedMaterial;
            visual.Configure(VisualArchetype.Player, visualRoot, new Renderer[] { renderer }, new[] { renderer.transform });
            for (var i = 0; i < 3; i++)
                Assert.That(visual.IsGameplayFree(out var reason), Is.True, reason);
            Assert.That(renderer.sharedMaterial, Is.SameAs(before));
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
        }

        private void AssertRejected(Type type, bool inactive)
        {
            visual.Configure(VisualArchetype.Dummy, visualRoot, new Renderer[] { renderer }, new[] { renderer.transform });
            var nested = Child(Child(visualRoot, "Nested"), "Offender");
            nested.gameObject.SetActive(!inactive);
            if (type == typeof(Cloth))
            {
                // Cloth requires a SkinnedMeshRenderer and a valid mesh, even on an inactive object.
                clothMesh = new Mesh
                {
                    vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                    triangles = new[] { 0, 1, 2 },
                    bindposes = new[] { Matrix4x4.identity },
                    boneWeights = new[]
                    {
                        new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                        new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                        new BoneWeight { boneIndex0 = 0, weight0 = 1f }
                    }
                };
                clothMesh.RecalculateNormals();
                var skin = nested.gameObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = clothMesh;
                skin.bones = new[] { nested };
                skin.rootBone = nested;
            }
            var offender = nested.gameObject.AddComponent(type);
            Assert.That(offender, Is.Not.Null, "The real Unity component must be present for this regression.");
            Assert.That(visual.IsGameplayFree(out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(nested.GetComponent(type), Is.SameAs(offender));
            StringAssert.Contains(type.Name, reason);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
