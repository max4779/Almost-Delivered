using AlmostDelivered.Gameplay.Payload;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PayloadEscortRadiusViewTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Escort radius test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ConfigureDrawsClosedHorizontalRingAtEscortRadius()
        {
            var view = root.AddComponent<PayloadEscortRadiusView>();

            view.Configure(null, 4.5f, null);

            var line = root.GetComponent<LineRenderer>();
            Assert.That(view.Radius, Is.EqualTo(4.5f));
            Assert.That(line.loop, Is.True);
            Assert.That(line.useWorldSpace, Is.False);
            Assert.That(line.positionCount, Is.GreaterThanOrEqualTo(48));
            for (var index = 0; index < line.positionCount; index++)
            {
                var point = line.GetPosition(index);
                Assert.That(point.y, Is.EqualTo(.08f).Within(.001f));
                Assert.That(new Vector2(point.x, point.z).magnitude, Is.EqualTo(4.5f).Within(.01f));
            }
        }
    }
}
