using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PlayerDamageFlashViewTests
    {
        private GameObject root;
        private ActorHealth health;
        private PlayerDamageFlashView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Damage flash test");
            health = new GameObject("Player", typeof(ActorHealth)).GetComponent<ActorHealth>();
            health.transform.SetParent(root.transform, false);
            var overlay = new GameObject("Damage Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PlayerDamageFlashView));
            overlay.transform.SetParent(root.transform, false);
            view = overlay.GetComponent<PlayerDamageFlashView>();
            view.Configure(health, overlay.GetComponent<Image>());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DamageFlashesRedThenFadesWithoutBlockingInput()
        {
            health.ApplyDamage(10f);

            Assert.That(view.CurrentAlpha, Is.EqualTo(.09f).Within(.001f));
            Assert.That(view.Overlay.color.r, Is.GreaterThan(.9f));
            Assert.That(view.Overlay.color.g, Is.GreaterThan(.2f));
            Assert.That(view.Overlay.raycastTarget, Is.False);

            view.Tick(.15f);
            Assert.That(view.CurrentAlpha, Is.InRange(.01f, .09f));

            view.Tick(1f);
            Assert.That(view.CurrentAlpha, Is.Zero);
        }

        [Test]
        public void HealingOrMaximumConfigurationDoesNotFlash()
        {
            health.ConfigureMaximum(150f);

            Assert.That(view.CurrentAlpha, Is.Zero);
        }
    }
}
