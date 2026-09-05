using Client.Adapters.AceOfShadows.Components;
using DCFApixels.DragonECS.Core;
using NUnit.Framework;

namespace Client.Adapters.Tests
{
    /// <summary>A copy of the face table is its own table, not a second name for the same array.</summary>
    /// <remarks>
    /// Rule 3 of adr-data-placement-is-decided-on-three-axes: a datum enters the world when it is
    /// copyable, and a struct holding an array is copyable only through the copy handler.
    /// </remarks>
    public sealed class CardArtCompTests
    {
        [Test]
        public void Copy_ClonesTheFaces_SoTheCopyOwnsItsOwnArray()
        {
            Assert.That(EcsComponentCopy<CardArtComp>.IsCustom, Is.True,
                "CardArtComp must declare its own copy: it holds an array.");

            var from = new CardArtComp { Back = 7, Faces = new[] { 1, 2, 3 } };
            var to = default(CardArtComp);

            EcsComponentCopy<CardArtComp>.CustomHandler.Copy(ref from, ref to);
            to.Faces[0] = 99;

            Assert.That(to.Back, Is.EqualTo(7));
            Assert.That(from.Faces[0], Is.EqualTo(1), "The source must not see a write to the copy.");
        }

        [Test]
        public void Copy_OfAnUnresolvedTable_StaysUnresolved()
        {
            var from = default(CardArtComp);
            var to = new CardArtComp { Faces = new[] { 4 } };

            EcsComponentCopy<CardArtComp>.CustomHandler.Copy(ref from, ref to);

            Assert.That(to.Faces, Is.Null);
        }
    }
}
