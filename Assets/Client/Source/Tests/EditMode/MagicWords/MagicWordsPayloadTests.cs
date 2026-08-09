using System.Collections.Generic;
using System.Linq;
using System.Text;
using DCFApixels.DragonECS;
using Game.Simulation.MagicWords;
using Game.Simulation.Tests.Fakes;
using NUnit.Framework;

namespace Game.Simulation.Tests.MagicWords
{
    public sealed class MagicWordsPayloadTests : MagicWordsTestFixture
    {
        [Test]
        public void RealPayload_CreatesOrderedLinesAndFirstAppearanceSpeakers()
        {
            _LoadRealPayload();

            var lineIndices = new List<int>();
            foreach (var entityId in World.Where(out LineAspect aspect))
                lineIndices.Add(aspect.Lines.Read(entityId).Index);

            var speakerNames = new List<string>();
            foreach (var entityId in World.Where(out SpeakerAspect aspect))
                speakerNames.Add(aspect.Speakers.Read(entityId).Name);

            Assert.That(lineIndices, Is.EqualTo(Enumerable.Range(0, 17)));
            Assert.That(speakerNames, Is.EqualTo(new[] { "Sheldon", "Leonard", "Penny", "Neighbour" }));
        }

        [Test]
        public void RealPayload_UsesFirstDuplicateAndRepresentsMissingAvatar()
        {
            _LoadRealPayload();

            var speakers = _SpeakerEntities();
            var avatars = World.GetPool<AvatarComp>();
            var loads = World.GetPool<AvatarLoadComp>();
            var sheldonId = speakers["Sheldon"];

            Assert.That(avatars.Read(sheldonId).Url, Does.StartWith("https://api.dicebear.com/9.x/personas/png"));
            Assert.That(avatars.Read(sheldonId).Side, Is.EqualTo(AvatarSide.Left));
            Assert.That(loads.Read(speakers["Neighbour"]).State, Is.EqualTo(AvatarLoadState.Missing));
            Assert.That(avatars.Has(speakers["Neighbour"]), Is.False);
            Assert.That(speakers.ContainsKey("Nobody"), Is.False);
            Assert.That(Log.CountOf(FakeLog.Level.Warn), Is.EqualTo(1));
            Assert.That(
                Log.OfLevel(FakeLog.Level.Warn).Single().Message,
                Does.Contain("Duplicate avatar for 'Sheldon'"));
        }

        [Test]
        public void RealPayload_PreservesAllEmojiNamesAndUnicodeText()
        {
            var payload = LoadPayload();
            DialogueSource.Payload = payload;
            _Load();

            var emojiNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var entityId in World.Where(out LineAspect aspect))
            {
                ref readonly var line = ref aspect.Lines.Read(entityId);
                ref readonly var text = ref aspect.Texts.Read(entityId);
                var rebuilt = new StringBuilder();

                foreach (var segment in text.Segments)
                {
                    if (segment.Kind == SegmentKind.Emoji)
                    {
                        emojiNames.Add(segment.Value);
                        rebuilt.Append('{').Append(segment.Value).Append('}');
                    }
                    else
                    {
                        rebuilt.Append(segment.Value);
                    }
                }

                Assert.That(rebuilt.ToString(), Is.EqualTo(payload.dialogue[line.Index].text));
            }

            Assert.That(
                emojiNames.SetEquals(
                    new[] { "satisfied", "intrigued", "neutral", "affirmative", "laughing", "win" }),
                Is.True);
            Assert.That(emojiNames, Has.Count.EqualTo(6));
            Assert.That(payload.dialogue[1].text, Does.Contain("That’s"));
        }

        [Test]
        public void RealPayload_SetsReadyStateAndCapturedCounts()
        {
            _LoadRealPayload();

            ref readonly var state = ref World.Get<DialogueStateComp>();
            Assert.That(state.State, Is.EqualTo(DialogueLoadState.Ready));
            Assert.That(state.LineCount, Is.EqualTo(17));
            Assert.That(state.SpeakerCount, Is.EqualTo(4));
        }

        private void _LoadRealPayload()
        {
            DialogueSource.Payload = LoadPayload();
            _Load();
        }

        private Dictionary<string, int> _SpeakerEntities()
        {
            var result = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var entityId in World.Where(out SpeakerAspect aspect))
                result.Add(aspect.Speakers.Read(entityId).Name, entityId);

            return result;
        }

        private sealed class LineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
            public EcsPool<DialogueTextComp> Texts = Inc;
        }

        private sealed class SpeakerAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
        }
    }
}
