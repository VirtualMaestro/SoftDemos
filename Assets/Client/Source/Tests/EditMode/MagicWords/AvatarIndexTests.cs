using System.Linq;
using Game.Simulation.MagicWords;
using Game.Simulation.Tests.Fakes;
using NUnit.Framework;

namespace Game.Simulation.Tests.MagicWords
{
    public sealed class AvatarIndexTests
    {
        [Test]
        public void NullArray_ProducesOneWarning()
        {
            var log = new FakeLog();
            var index = new AvatarIndex(null, log);

            Assert.That(index.TryGet("Sheldon", out _, out _), Is.False);
            Assert.That(log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        [Test]
        public void InvalidEntries_AreDiscardedAndWarned()
        {
            var log = new FakeLog();
            var index = new AvatarIndex(
                new AvatarDto[]
                {
                    null,
                    new() { name = " ", url = "url", position = "left" }
                },
                log);

            Assert.That(index.TryGet(" ", out _, out _), Is.False);
            Assert.That(log.CountOf(FakeLog.Level.Warn), Is.EqualTo(2));
        }

        [Test]
        public void DuplicateName_KeepsFirstUrlAndSide()
        {
            var log = new FakeLog();
            var index = new AvatarIndex(
                new[]
                {
                    new AvatarDto { name = "Sheldon", url = "first", position = "left" },
                    new AvatarDto { name = "Sheldon", url = "second", position = "right" }
                },
                log);

            Assert.That(index.TryGet("Sheldon", out var url, out var side), Is.True);
            Assert.That(url, Is.EqualTo("first"));
            Assert.That(side, Is.EqualTo(AvatarSide.Left));
            Assert.That(log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
            Assert.That(log.OfLevel(FakeLog.Level.Warn).Single().Message, Does.Contain("first entry wins"));
        }

        [Test]
        public void Lookup_IsOrdinalAndCaseSensitive()
        {
            var index = new AvatarIndex(
                new[] { new AvatarDto { name = "Sheldon", url = "url", position = "left" } },
                new FakeLog());

            Assert.That(index.TryGet("sheldon", out _, out _), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("middle")]
        public void InvalidPosition_FallsBackToLeftAndWarns(string position)
        {
            var log = new FakeLog();
            var index = new AvatarIndex(
                new[] { new AvatarDto { name = "Penny", url = "url", position = position } },
                log);

            Assert.That(index.TryGet("Penny", out _, out var side), Is.True);
            Assert.That(side, Is.EqualTo(AvatarSide.Left));
            Assert.That(log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void MissingUrl_IsIndexedAsAbsentAndWarns(string url)
        {
            var log = new FakeLog();
            var index = new AvatarIndex(
                new[] { new AvatarDto { name = "Neighbour", url = url, position = "right" } },
                log);

            Assert.That(index.TryGet("Neighbour", out _, out _), Is.False);
            Assert.That(log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }
    }
}
