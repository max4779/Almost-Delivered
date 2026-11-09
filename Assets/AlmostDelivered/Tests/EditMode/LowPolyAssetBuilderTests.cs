using System;
using System.Collections.Generic;
using System.Linq;
using AlmostDelivered.Presentation;
using AlmostDelivered.Presentation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlmostDelivered.Tests.EditMode
{
    public class LowPolyAssetBuilderTests
    {
        static VisualArchetype[] Types => (VisualArchetype[])Enum.GetValues(typeof(VisualArchetype));
        [Test] public void CreateOrUpdateAll_CreatesEveryArchetypeWithBuiltInShader() {
            LowPolyAssetFactory.CreateOrUpdateAll();
            foreach (var type in Types) { var prefab = LowPolyAssetFactory.LoadPrefab(type); Assert.NotNull(prefab, type.ToString()); foreach (var r in prefab.GetComponentsInChildren<Renderer>()) CollectionAssert.Contains(new[]{"Standard", "Unlit/Color"}, r.sharedMaterial.shader.name); }
        }
        [Test] public void GeneratedPrefabs_HaveOneVisualRootAndNoGameplayComponents() {
            LowPolyAssetFactory.CreateOrUpdateAll(); foreach (var type in Types) { var prefab = LowPolyAssetFactory.LoadPrefab(type); Assert.AreEqual("VisualRoot", prefab.name); var visual = prefab.GetComponent<LowPolyVisual>(); Assert.NotNull(visual); Assert.True(visual.IsGameplayFree(out var reason), reason); Assert.AreEqual(1, prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "VisualRoot")); }
        }
        [Test] public void GeneratedPrefabs_UseOnlyGeneratedSharedMaterials() {
            LowPolyAssetFactory.CreateOrUpdateAll(); foreach (var type in Types) foreach (var r in LowPolyAssetFactory.LoadPrefab(type).GetComponentsInChildren<Renderer>()) foreach (var m in r.sharedMaterials) { Assert.NotNull(m); StringAssert.StartsWith("Assets/AlmostDelivered/Art/Materials/Generated/", AssetDatabase.GetAssetPath(m)); }
        }
        [Test] public void GeneratedRoleSilhouettes_HaveRequiredNamedPartsAndDistinctBounds() {
            LowPolyAssetFactory.CreateOrUpdateAll();
            foreach (var type in Types.Take(7)) { var p = LowPolyAssetFactory.LoadPrefab(type); foreach (var name in new[]{"Body", "Head", "Weapon"}) Assert.True(Has(p,name), type+" "+name); }
            foreach (var pair in new[]{(VisualArchetype.Shield,"Shield"),(VisualArchetype.Ranged,"RangedWeapon"),(VisualArchetype.Charger,"Horns"),(VisualArchetype.Support,"Totem"),(VisualArchetype.Commander,"Cape"),(VisualArchetype.Commander,"Banner"),(VisualArchetype.Commander,"WarHorn"),(VisualArchetype.Payload,"CargoHull"),(VisualArchetype.Payload,"EnergyCore"),(VisualArchetype.Payload,"HoverShadow"),(VisualArchetype.ObjectiveBeacon,"Beacon"),(VisualArchetype.ObjectiveBeacon,"RuneRing")}) Assert.True(Has(LowPolyAssetFactory.LoadPrefab(pair.Item1),pair.Item2), pair.ToString());
            CollectionAssert.AreEquivalent(new[]{"ThrusterRearLeft", "ThrusterFrontLeft", "ThrusterRearRight", "ThrusterFrontRight"}, LowPolyAssetFactory.LoadPrefab(VisualArchetype.Payload).GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Thruster", StringComparison.Ordinal)).Select(t=>t.name));
            Assert.That(LowPolyAssetFactory.LoadPrefab(VisualArchetype.Payload).GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Wheel", StringComparison.Ordinal) || t.name == "Cart"), Is.False);
            Assert.GreaterOrEqual(Size(VisualArchetype.Shield).x, Size(VisualArchetype.Ranged).x * 1.2f); foreach (var t in Types.Take(6)) Assert.Greater(Size(VisualArchetype.Commander).y, Size(t).y); Assert.Greater(Size(VisualArchetype.Charger).z, Size(VisualArchetype.Support).z); Assert.Greater(Size(VisualArchetype.Payload).magnitude, 0);
        }
        [Test] public void CreateOrUpdateAll_RepeatedRunPreservesGuidsAndChildCounts() {
            LowPolyAssetFactory.CreateOrUpdateAll(); var guids = Types.Select(t=>AssetDatabase.AssetPathToGUID(LowPolyAssetFactory.GetPrefabPath(t))).ToArray(); var counts = Types.Select(t=>LowPolyAssetFactory.LoadPrefab(t).GetComponentsInChildren<Transform>().Length).ToArray(); LowPolyAssetFactory.CreateOrUpdateAll(); CollectionAssert.AreEqual(guids, Types.Select(t=>AssetDatabase.AssetPathToGUID(LowPolyAssetFactory.GetPrefabPath(t))).ToArray()); CollectionAssert.AreEqual(counts, Types.Select(t=>LowPolyAssetFactory.LoadPrefab(t).GetComponentsInChildren<Transform>().Length).ToArray());
        }
        [Test] public void CreateOrUpdateAll_RepeatedRunPreservesEachPayloadThrusterLocalIds() {
            LowPolyAssetFactory.CreateOrUpdateAll();
            var before = PayloadThrusterLocalIds();
            Assert.AreEqual(4, before.Count);

            LowPolyAssetFactory.CreateOrUpdateAll();
            var after = PayloadThrusterLocalIds();

            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (var thruster in before)
                CollectionAssert.AreEqual(thruster.Value, after[thruster.Key], "Thruster at " + thruster.Key + " lost its serialized object/component identities");
        }
        [Test] public void InstantiateVisual_ReplacesExistingVisualOnlyAndPreservesRootComponents() {
            LowPolyAssetFactory.CreateOrUpdateAll(); var root = GameObject.CreatePrimitive(PrimitiveType.Cube); try { var collider = root.GetComponent<Collider>(); var renderer = root.GetComponent<Renderer>(); var other = new GameObject("Other"); other.transform.SetParent(root.transform); LowPolyAssetFactory.InstantiateVisual(root, VisualArchetype.Player); var visual = LowPolyAssetFactory.InstantiateVisual(root, VisualArchetype.Shield); Assert.AreEqual(1, root.transform.Cast<Transform>().Count(t=>t.name == "VisualRoot")); Assert.AreSame(collider,root.GetComponent<Collider>()); Assert.AreSame(renderer,root.GetComponent<Renderer>()); Assert.NotNull(root.transform.Find("Other")); Assert.AreEqual(VisualArchetype.Shield, visual.Archetype); } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void MissingShader_ThrowsBeforeWritingAnyAsset() {
            var paths = AssetDatabase.GetAllAssetPaths(); Assert.Throws<InvalidOperationException>(()=>LowPolyAssetFactory.CreateOrUpdateAll(name=>name == "Unlit/Color" ? null : Shader.Find(name))); CollectionAssert.AreEqual(paths, AssetDatabase.GetAllAssetPaths());
        }
        [Test] public void GeneratedCharacterArms_PivotAtShouldersWithRelativeGeometry() {
            LowPolyAssetFactory.CreateOrUpdateAll();
            foreach (var type in Types.Take(7)) {
                var body = LowPolyAssetFactory.LoadPrefab(type).transform.Find("BodyPivot");
                foreach (var pair in new[]{("LeftArmPivot", -.48f), ("RightArmPivot", .48f)}) {
                    var pivot = body.Find(pair.Item1);
                    Assert.NotNull(pivot, type + " " + pair.Item1);
                    Assert.That(pivot.localPosition.x, Is.EqualTo(pair.Item2).Within(.001f));
                    Assert.That(pivot.localPosition.y, Is.EqualTo(1.3f).Within(.001f));
                    Assert.Greater(pivot.childCount, 0);
                    foreach (Transform part in pivot) {
                        Assert.Less(Mathf.Abs(part.localPosition.y), .8f, type + " " + part.name);
                        Assert.Less(Mathf.Abs(part.localPosition.x), .8f, type + " " + part.name);
                    }
                }
            }
        }
        static bool Has(GameObject p,string n) => p.GetComponentsInChildren<Transform>().Any(t=>t.name==n);
        static Dictionary<Vector3, long[]> PayloadThrusterLocalIds() {
            return LowPolyAssetFactory.LoadPrefab(VisualArchetype.Payload).GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("Thruster", StringComparison.Ordinal))
                .ToDictionary(t => t.localPosition, t => new UnityEngine.Object[]{t.gameObject, t, t.GetComponent<MeshFilter>(), t.GetComponent<MeshRenderer>()}
                    .Select(part => {
                        Assert.True(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(part, out string guid, out long localId));
                        Assert.AreEqual(AssetDatabase.AssetPathToGUID(LowPolyAssetFactory.GetPrefabPath(VisualArchetype.Payload)), guid);
                        return localId;
                    }).ToArray());
        }
        static Vector3 Size(VisualArchetype t) { var rs=LowPolyAssetFactory.LoadPrefab(t).GetComponentsInChildren<Renderer>(); var b=rs[0].bounds; foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b.size; }
    }
}
