using System;
using DCFApixels.DragonECS;
using Game.Simulation.MagicWords;
using Game.Simulation.Tests.Fakes;
using NUnit.Framework;

namespace Game.Simulation.Tests.MagicWords
{
    public sealed class MagicWordsMalformedPayloadTests : MagicWordsTestFixture
    {
        [Test]
        public void DoneRequestWithNullPayload_FailsAndLogsOneError()
        {
            _LoadWithoutThrow(null);

            Assert.That(World.Get<DialogueStateComp>().State, Is.EqualTo(DialogueLoadState.Failed));
            Assert.That(Log.CountOf(FakeLog.Level.Error), Is.EqualTo(1));
        }

        [Test]
        public void NullDialogue_IsReadyAndEmpty()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = null,
                avatars = Array.Empty<AvatarDto>()
            });

            _AssertReadyWithNoLines();
        }

        [Test]
        public void EmptyDialogue_IsReadyAndEmpty()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = Array.Empty<DialogueLineDto>(),
                avatars = Array.Empty<AvatarDto>()
            });

            _AssertReadyWithNoLines();
        }

        [Test]
        public void NullAvatarArray_MarksEverySpeakerMissing()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = "Penny", text = "Hello" } },
                avatars = null
            });

            foreach (var entityId in World.Where(out SpeakerAspect aspect))
            {
                Assert.That(
                    aspect.Loads.Read(entityId).State,
                    Is.EqualTo(AvatarLoadState.Missing));
            }
        }

        [Test]
        public void NullDialogueEntry_IsSkippedAndWarned()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new DialogueLineDto[] { null },
                avatars = Array.Empty<AvatarDto>()
            });

            Assert.That(World.Get<DialogueStateComp>().LineCount, Is.Zero);
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        [Test]
        public void NullAvatarEntry_IsSkippedAndWarned()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = "Penny", text = "Hello" } },
                avatars = new AvatarDto[] { null }
            });

            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase(" ")]
        public void MissingSpeakerName_IsSkippedAndWarned(string name)
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = name, text = "Hello" } },
                avatars = Array.Empty<AvatarDto>()
            });

            Assert.That(World.Get<DialogueStateComp>().LineCount, Is.Zero);
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        [Test]
        public void NullText_IsKeptWithNoSegments()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = "Penny", text = null } },
                avatars = Array.Empty<AvatarDto>()
            });

            foreach (var entityId in World.Where(out LineAspect aspect))
                Assert.That(aspect.Texts.Read(entityId).Segments, Is.Empty);
            Assert.That(World.Get<DialogueStateComp>().LineCount, Is.EqualTo(1));
        }

        [Test]
        public void NullAvatarUrl_MarksSpeakerMissingAndWarns()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = "Penny", text = "Hello" } },
                avatars = new[]
                {
                    new AvatarDto { name = "Penny", url = null, position = "right" }
                }
            });

            foreach (var entityId in World.Where(out SpeakerAspect aspect))
                Assert.That(aspect.Loads.Read(entityId).State, Is.EqualTo(AvatarLoadState.Missing));
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        [Test]
        public void InvalidAvatarPosition_FallsBackToLeftAndWarns()
        {
            _LoadWithoutThrow(new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = "Penny", text = "Hello" } },
                avatars = new[]
                {
                    new AvatarDto { name = "Penny", url = "url", position = "sideways" }
                }
            });

            foreach (var entityId in World.Where(out SpeakerAspect aspect))
                Assert.That(aspect.Avatars.Read(entityId).Side, Is.EqualTo(AvatarSide.Left));
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
        }

        private void _LoadWithoutThrow(DialoguePayload payload)
        {
            DialogueSource.Payload = payload;
            Assert.DoesNotThrow(_Load);
        }

        private void _AssertReadyWithNoLines()
        {
            ref readonly var state = ref World.Get<DialogueStateComp>();
            Assert.That(state.State, Is.EqualTo(DialogueLoadState.Ready));
            Assert.That(state.LineCount, Is.Zero);
        }

        private sealed class SpeakerAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
            public EcsPool<AvatarLoadComp> Loads = Inc;
            public EcsPool<AvatarComp> Avatars = Opt;
        }

        private sealed class LineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
            public EcsPool<DialogueTextComp> Texts = Inc;
        }
    }
}
