using System.Collections.Generic;
using System.Linq;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using Client.Simulation.MagicWords.Payload;
using Client.Simulation.Tests.Fakes;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.MagicWords
{
    public sealed class DialoguePlaybackTests : MagicWordsTestFixture
    {
        private const float LineIntervalSeconds = 0.1f;
        private const float Epsilon = 0.001f;

        [Test]
        public void Playback_RevealsImmediatelyThenByCadenceAcrossIndexGap()
        {
            _LoadPayload(new DialogueLineDto[]
            {
                _Line("Alpha", "First"),
                null,
                _Line("Alpha", "Second"),
                _Line("Missing", "Third"),
                _Line("Beta", "Fourth")
            });

            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0 }));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(1));

            _Advance(LineIntervalSeconds - Epsilon);
            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0 }));

            _Advance(Epsilon * 2f);
            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(1));

            _Advance(LineIntervalSeconds + Epsilon);
            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(1));

            _Advance(LineIntervalSeconds + Epsilon);
            Assert.That(
                _VisibleIndices(),
                Is.EqualTo(new[] { 0, 2, 3, 4 }),
                World.Get<DialoguePlaybackComp>().ToString());
            Assert.That(
                World.Get<DialoguePlaybackComp>().IsComplete,
                Is.True,
                World.Get<DialoguePlaybackComp>().ToString());
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(2));
            Assert.That(
                Log.OfLevel(FakeLog.Level.Info).Any(x => x.Message.Contains("playback completed")),
                Is.True);
        }

        [Test]
        public void SkipQueuedWhileLoading_IsDiscardedAndCadenceStillPlays()
        {
            DialogueSource.CompleteAfterPolls = 3;
            _LoadPayload(new[]
            {
                _Line("Alpha", "First"),
                _Line("Alpha", "Second"),
                _Line("Beta", "Third")
            });
            Assert.That(World.Get<DialogueStateComp>().State,
                Is.Not.EqualTo(DialogueLoadState.Ready));

            World.GetPool<SkipDialogueCommand>().Add(World.NewEntity());
            for (var tick = 0;
                tick < 5 && World.Get<DialogueStateComp>().State != DialogueLoadState.Ready;
                tick++)
                _Tick();

            Assert.That(World.Get<DialogueStateComp>().State,
                Is.EqualTo(DialogueLoadState.Ready));
            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0 }));
            Assert.That(World.Get<DialoguePlaybackComp>().IsComplete, Is.False);

            _Advance(LineIntervalSeconds + Epsilon);

            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void Skip_RevealsEveryRemainingLineAndSecondSkipIsNoOp()
        {
            _LoadPayload(new[]
            {
                _Line("Alpha", "First"),
                _Line("Alpha", "Second"),
                _Line("Beta", "Third")
            });

            _Skip();

            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(World.Get<DialoguePlaybackComp>().IsComplete, Is.True);
            var completionLogs = _CompletionLogCount();

            _Skip();

            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_CompletionLogCount(), Is.EqualTo(completionLogs));
        }

        [Test]
        public void Reset_ClearsPlaybackAndReopenStartsFromFirstLine()
        {
            var lines = new[]
            {
                _Line("Alpha", "First"),
                _Line("Beta", "Second")
            };
            _LoadPayload(lines);
            _Advance(LineIntervalSeconds + Epsilon);
            Assert.That(World.Get<DialoguePlaybackComp>().IsComplete, Is.True);

            _Reset();

            Assert.That(
                World.Get<DialoguePlaybackComp>(),
                Is.EqualTo(default(DialoguePlaybackComp)));

            _LoadPayload(lines);

            Assert.That(_VisibleIndices(), Is.EqualTo(new[] { 0 }));
            Assert.That(World.Get<DialoguePlaybackComp>().VisibleLineCount, Is.EqualTo(1));
        }

        protected override MagicWordsConfig CreateConfig() =>
            new(lineIntervalSeconds: LineIntervalSeconds);

        private void _LoadPayload(DialogueLineDto[] lines)
        {
            DialogueSource.Payload = new DialoguePayload
            {
                dialogue = lines,
                avatars = new[]
                {
                    new AvatarDto { name = "Alpha", url = "alpha", position = "left" },
                    new AvatarDto { name = "Beta", url = "beta", position = "right" }
                }
            };
            _Load();
        }

        private void _Skip()
        {
            var entityId = World.NewEntity();
            World.GetPool<SkipDialogueCommand>().Add(entityId);
            _Tick();
        }

        private int[] _VisibleIndices()
        {
            var indices = new List<int>();
            foreach (var entityId in World.Where(out VisibleLineAspect aspect))
                indices.Add(aspect.Lines.Read(entityId).Index);

            indices.Sort();
            return indices.ToArray();
        }

        private int _CompletionLogCount() =>
            Log.OfLevel(FakeLog.Level.Info).Count(x => x.Message.Contains("playback completed"));

        private static DialogueLineDto _Line(string name, string text) =>
            new() { name = name, text = text };

        private sealed class VisibleLineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
            public EcsTagPool<LineVisibleTag> Visible = Inc;
        }
    }
}
