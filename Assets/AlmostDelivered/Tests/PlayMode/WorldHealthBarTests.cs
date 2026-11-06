using System.Collections;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class WorldHealthBarTests
    {
        private GameObject actor;
        private ActorHealth health;
        private GameObject cameraObject;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Health bar actor");
            health = actor.AddComponent<ActorHealth>();
            cameraObject = new GameObject("Health bar camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AttachCreatesOneWorldSpaceBarAtRequestedOffset()
        {
            var offset = new Vector3(0f, 2.5f, 0f);

            var bar = WorldHealthBar.Attach(actor, offset);

            Assert.That(bar.transform.parent, Is.EqualTo(actor.transform));
            Assert.That(bar.transform.localPosition, Is.EqualTo(offset));
            Assert.That(bar.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(actor.GetComponentsInChildren<WorldHealthBar>(true), Has.Length.EqualTo(1));
            Assert.That(bar.GetComponentsInChildren<Image>(true), Has.Length.EqualTo(2));
            Assert.That(bar.FillAmount, Is.EqualTo(1f));
        }

        [Test]
        public void DamageUpdatesFillFromHealthChangeEvent()
        {
            var bar = WorldHealthBar.Attach(actor, Vector3.up * 2f);

            health.ApplyDamage(25f);

            Assert.That(bar.FillAmount, Is.EqualTo(.75f).Within(.0001f));
        }

        [Test]
        public void DamageShrinksVisibleFillGeometryFromTheRightEdge()
        {
            var bar = WorldHealthBar.Attach(actor, Vector3.up * 2f);
            var fill = bar.transform.Find("Fill").GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            var fullCorners = new Vector3[4];
            fill.GetWorldCorners(fullCorners);
            var fullWidth = Vector3.Distance(fullCorners[0], fullCorners[3]);

            health.ApplyDamage(25f);
            Canvas.ForceUpdateCanvases();
            var damagedCorners = new Vector3[4];
            fill.GetWorldCorners(damagedCorners);

            Assert.That(Vector3.Distance(damagedCorners[0], fullCorners[0]), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(damagedCorners[0], damagedCorners[3]) / fullWidth, Is.EqualTo(.75f).Within(.01f));
        }

        [Test]
        public void RepeatedAttachReturnsExistingBarWithoutDuplicates()
        {
            var first = WorldHealthBar.Attach(actor, Vector3.up * 2f);
            var second = WorldHealthBar.Attach(actor, Vector3.up * 3f);

            Assert.That(second, Is.SameAs(first));
            Assert.That(actor.GetComponentsInChildren<WorldHealthBar>(true), Has.Length.EqualTo(1));
            Assert.That(first.transform.localPosition, Is.EqualTo(Vector3.up * 3f));
        }

        [Test]
        public void DeathHidesBarImmediately()
        {
            var bar = WorldHealthBar.Attach(actor, Vector3.up * 2f);

            health.ApplyDamage(health.MaxHealth);

            Assert.That(bar.gameObject.activeSelf, Is.False);
            Assert.That(bar.FillAmount, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator BarFacesMainCameraAfterLateUpdate()
        {
            cameraObject.transform.position = new Vector3(4f, 5f, -10f);
            var bar = WorldHealthBar.Attach(actor, Vector3.up * 2f);

            yield return null;

            var expectedForward = (bar.transform.position - cameraObject.transform.position).normalized;
            Assert.That(Vector3.Dot(bar.transform.forward, expectedForward), Is.GreaterThan(.999f));
        }
    }
}
