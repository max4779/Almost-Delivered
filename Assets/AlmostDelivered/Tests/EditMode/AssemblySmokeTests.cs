using NUnit.Framework;

namespace AlmostDelivered.Tests.EditMode
{
    public sealed class AssemblySmokeTests
    {
        [Test]
        public void RuntimeAssembly_IsLoadable()
        {
            Assert.That(
                typeof(AlmostDelivered.RuntimeAssemblyMarker).Assembly.GetName().Name,
                Is.EqualTo("AlmostDelivered.Runtime"));
        }
    }
}
