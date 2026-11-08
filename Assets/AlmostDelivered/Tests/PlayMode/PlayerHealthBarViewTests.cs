using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PlayerHealthBarViewTests
    {
        private GameObject root;
        private ActorHealth health;
        private PlayerHealthBarView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Health bar test", typeof(RectTransform), typeof(Image), typeof(PlayerHealthBarView));
            health = new GameObject("Player", typeof(ActorHealth)).GetComponent<ActorHealth>();
            health.transform.SetParent(root.transform, false);
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(root.transform, false);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            view = root.GetComponent<PlayerHealthBarView>();
            view.Configure(health, fillObject.GetComponent<Image>(), labelObject.GetComponent<Text>(), root.GetComponent<RectTransform>());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void HealthBarTracksHealthChangesColorAndValueLabel()
        {
            Assert.That(view.NormalizedHealth, Is.EqualTo(1f));
            Assert.That(view.Fill.color.r, Is.GreaterThan(view.Fill.color.b));
            Assert.That(view.Fill.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(view.Fill.rectTransform.localScale.x, Is.EqualTo(1f));
            Assert.That(view.Label.text, Does.Contain("100 / 100"));

            health.ApplyDamage(50f);
            Assert.That(view.NormalizedHealth, Is.EqualTo(.5f));
            Assert.That(view.Fill.rectTransform.localScale.x, Is.EqualTo(.5f));
            Assert.That(view.Fill.color.r, Is.GreaterThan(.7f));
            Assert.That(view.Label.text, Does.Contain("50 / 100"));

            health.ApplyDamage(30f);
            Assert.That(view.NormalizedHealth, Is.EqualTo(.2f));
            Assert.That(view.Fill.rectTransform.localScale.x, Is.EqualTo(.2f));
            Assert.That(view.Fill.color.r, Is.GreaterThan(view.Fill.color.g));
        }

        [Test]
        public void DamageBrieflyPulsesTheBarWithoutChangingItsAnchor()
        {
            health.ApplyDamage(10f);
            view.Tick(.01f);

            Assert.That(root.transform.localScale.x, Is.GreaterThan(1f));

            view.Tick(1f);
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
        }
    }
}
