using Client.Adapters.Layout;
using NUnit.Framework;

namespace Client.Adapters.Tests
{
    public sealed class LayoutModeTests
    {
        [TestCase(9f / 16f, LayoutMode.Portrait)]
        [TestCase(3f / 4f, LayoutMode.Portrait)]
        [TestCase(1f, LayoutMode.Landscape)]
        [TestCase(16f / 9f, LayoutMode.Landscape)]
        [TestCase(21f / 9f, LayoutMode.Landscape)]
        public void FromAspect_ClassifiesPortraitAndLandscape(float aspect, LayoutMode expected)
        {
            Assert.That(LayoutModes.FromAspect(aspect), Is.EqualTo(expected));
        }
    }
}
