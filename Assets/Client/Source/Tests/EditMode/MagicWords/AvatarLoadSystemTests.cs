using System.Collections.Generic;
using System.Linq;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using Client.Simulation.MagicWords.Payload;
using Client.Simulation.Tests.Fakes;
using DCFApixels.DragonECS;
using NUnit.Framework;

namespace Client.Simulation.Tests.MagicWords
{
    public sealed class AvatarLoadSystemTests : MagicWordsTestFixture
    {
        [Test]
        public void Avatar_TransitionsFromNotRequestedThroughLoadingToReady()
        {
            ImageSource.CompleteAfterPolls = 3;
            var speakers = _LoadRealPayload();
            var leonardId = speakers["Leonard"];
            var expectedUrl = World.GetPool<AvatarComp>().Read(leonardId).Url;

            Assert.That(_State(leonardId), Is.EqualTo(AvatarLoadState.NotRequested));
            _RequestAvatar(leonardId);
            Assert.That(_State(leonardId), Is.EqualTo(AvatarLoadState.Loading));
            _Tick();
            Assert.That(_State(leonardId), Is.EqualTo(AvatarLoadState.Loading));
            _Tick();
            Assert.That(_State(leonardId), Is.EqualTo(AvatarLoadState.Ready));

            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(2));
            Assert.That(ImageSource.LoadCalls[1], Is.EqualTo(("Leonard", expectedUrl)));
        }

        [Test]
        public void FailedRequest_LogsAndReleases()
        {
            ImageSource.TerminalStatus = AsyncOpStatus.Failed;
            var speakers = _LoadRealPayload();
            var pennyId = speakers["Penny"];

            _RequestAvatar(pennyId);

            Assert.That(_State(pennyId), Is.EqualTo(AvatarLoadState.Failed));
            Assert.That(ImageSource.OpenRequestCount, Is.Zero);
            Assert.That(Log.CountOf(FakeLog.Level.Error), Is.EqualTo(2));
            Assert.That(Log.OfLevel(FakeLog.Level.Error).Any(x => x.Message.Contains("Penny")), Is.True);
        }

        [Test]
        public void DoneWithoutHandle_IsFailedAndReleased()
        {
            ImageSource.ReturnZeroHandle = true;
            var speakers = _LoadRealPayload();
            var leonardId = speakers["Leonard"];

            _RequestAvatar(leonardId);

            Assert.That(_State(leonardId), Is.EqualTo(AvatarLoadState.Failed));
            Assert.That(ImageSource.OpenRequestCount, Is.Zero);
            Assert.That(Log.CountOf(FakeLog.Level.Error), Is.EqualTo(2));
        }

        [Test]
        public void MissingSpeaker_DoesNotStartRequestAndSurvivesCommandConsumption()
        {
            var speakers = _LoadRealPayload();
            var neighbourId = speakers["Neighbour"];
            var loadCount = ImageSource.LoadCalls.Count;

            _RequestAvatar(neighbourId);

            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(loadCount));
            Assert.That(World.GetPool<SpeakerComp>().Has(neighbourId), Is.True);
            Assert.That(World.GetPool<SpeakerComp>().Read(neighbourId).Name, Is.EqualTo("Neighbour"));
            Assert.That(_State(neighbourId), Is.EqualTo(AvatarLoadState.Missing));
        }

        [Test]
        public void DuplicateCommands_DoNotStartSecondRequest()
        {
            ImageSource.CompleteAfterPolls = 3;
            var speakers = _LoadRealPayload();
            var sheldonId = speakers["Sheldon"];

            _RequestAvatar(sheldonId);
            _RequestAvatar(sheldonId);
            _Tick();
            Assert.That(_State(sheldonId), Is.EqualTo(AvatarLoadState.Ready));
            _RequestAvatar(sheldonId);
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(1));

            ImageSource.CompleteAfterPolls = 1;
            ImageSource.TerminalStatus = AsyncOpStatus.Failed;
            var pennyId = speakers["Penny"];
            _RequestAvatar(pennyId);
            _RequestAvatar(pennyId);
            Assert.That(_State(pennyId), Is.EqualTo(AvatarLoadState.Failed));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public void PipelineDestroy_ReleasesReadyAndInflightRequests()
        {
            var speakers = _LoadRealPayload();
            _OpenReadyAndInflightRequests(speakers);

            Assert.That(ImageSource.OpenRequestCount, Is.EqualTo(2));
            _DestroyPipeline();

            Assert.That(ImageSource.OpenRequestCount, Is.Zero);
        }

        [Test]
        public void Reset_ReleasesAnInflightDialogueRequest()
        {
            DialogueSource.CompleteAfterPolls = 3;
            DialogueSource.Payload = LoadPayload();
            _Load();
            Assert.That(DialogueSource.OpenRequestCount, Is.EqualTo(1));

            _Reset();

            Assert.That(DialogueSource.OpenRequestCount, Is.Zero);
            Assert.That(World.Get<DialogueStateComp>(), Is.EqualTo(default(DialogueStateComp)));
        }

        [Test]
        public void PipelineDestroy_ReleasesAnInflightDialogueRequest()
        {
            DialogueSource.CompleteAfterPolls = 3;
            DialogueSource.Payload = LoadPayload();
            _Load();
            Assert.That(DialogueSource.OpenRequestCount, Is.EqualTo(1));

            _DestroyPipeline();

            Assert.That(DialogueSource.OpenRequestCount, Is.Zero);
        }

        [Test]
        public void Reset_ReleasesRequestsDeletesEntitiesAndCanReopen()
        {
            var speakers = _LoadRealPayload();
            _OpenReadyAndInflightRequests(speakers);

            _Reset();

            Assert.That(ImageSource.OpenRequestCount, Is.Zero);
            Assert.That(_CountSpeakers(), Is.Zero);
            Assert.That(_CountLines(), Is.Zero);
            Assert.That(World.Get<DialogueStateComp>(), Is.EqualTo(default(DialogueStateComp)));

            DialogueSource.Payload = LoadPayload();
            _Load();

            Assert.That(World.Get<DialogueStateComp>().LineCount, Is.EqualTo(17));
            Assert.That(_CountLines(), Is.EqualTo(17));
        }

        [Test]
        public void Speakers_LoadIndependently()
        {
            var speakers = _LoadRealPayload();
            ImageSource.CompleteAfterPolls = 1;
            _RequestAvatar(speakers["Sheldon"]);
            ImageSource.CompleteAfterPolls = 100;
            _RequestAvatar(speakers["Penny"]);

            Assert.That(_State(speakers["Sheldon"]), Is.EqualTo(AvatarLoadState.Ready));
            Assert.That(_State(speakers["Penny"]), Is.EqualTo(AvatarLoadState.Loading));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public void ReloadReadySpeaker_ReleasesOldRequestAndReloadsInOneTick()
        {
            ImageSource.CompleteAfterPolls = 1;
            var speakers = _LoadRealPayload();
            var sheldonId = speakers["Sheldon"];
            var oldRequestId = World.GetPool<AvatarLoadComp>().Read(sheldonId).RequestId;

            _Reload();

            ref readonly var load = ref World.GetPool<AvatarLoadComp>().Read(sheldonId);
            Assert.That(load.State, Is.EqualTo(AvatarLoadState.Ready));
            Assert.That(load.RequestId, Is.Not.EqualTo(oldRequestId));
            Assert.That(ImageSource.ReleaseCalls, Does.Contain(oldRequestId));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public void ReloadLoadingSpeaker_ReleasesInflightRequestAndReloadsInOneTick()
        {
            ImageSource.CompleteAfterPolls = 100;
            var speakers = _LoadRealPayload();
            var sheldonId = speakers["Sheldon"];
            var oldRequestId = World.GetPool<AvatarLoadComp>().Read(sheldonId).RequestId;

            _Reload();

            ref readonly var load = ref World.GetPool<AvatarLoadComp>().Read(sheldonId);
            Assert.That(load.State, Is.EqualTo(AvatarLoadState.Loading));
            Assert.That(load.RequestId, Is.Not.EqualTo(oldRequestId));
            Assert.That(ImageSource.ReleaseCalls, Does.Contain(oldRequestId));
            Assert.That(ImageSource.LoadCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public void ReloadMissingSpeaker_IsNoOp()
        {
            DialogueSource.Payload = new DialoguePayload
            {
                dialogue = new[] { new DialogueLineDto { name = "Missing", text = "Hello" } },
                avatars = System.Array.Empty<AvatarDto>()
            };
            _Load();

            _Reload();

            Assert.That(ImageSource.LoadCalls, Is.Empty);
            Assert.That(ImageSource.ReleaseCalls, Is.Empty);
            Assert.That(Log.CountOf(FakeLog.Level.Error), Is.Zero);
        }

        [Test]
        public void ReloadWithoutDialogue_IsNoOp()
        {
            _Reload();

            Assert.That(ImageSource.LoadCalls, Is.Empty);
            Assert.That(ImageSource.ReleaseCalls, Is.Empty);
            Assert.That(Log.CountOf(FakeLog.Level.Error), Is.Zero);
        }

        private Dictionary<string, int> _LoadRealPayload()
        {
            DialogueSource.Payload = LoadPayload();
            _Load();

            var result = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var entityId in World.Where(out SpeakerAspect aspect))
                result.Add(aspect.Speakers.Read(entityId).Name, entityId);

            return result;
        }

        private void _OpenReadyAndInflightRequests(IReadOnlyDictionary<string, int> speakers)
        {
            ImageSource.CompleteAfterPolls = 1;
            _RequestAvatar(speakers["Sheldon"]);
            ImageSource.CompleteAfterPolls = 3;
            _RequestAvatar(speakers["Penny"]);
            Assert.That(_State(speakers["Sheldon"]), Is.EqualTo(AvatarLoadState.Ready));
            Assert.That(_State(speakers["Penny"]), Is.EqualTo(AvatarLoadState.Loading));
        }

        private void _RequestAvatar(int speakerEntityId)
        {
            World.GetPool<RequestAvatarCommand>().Add(speakerEntityId);
            Pipeline.Run();
        }

        private void _Reload()
        {
            var entityId = World.NewEntity();
            World.GetPool<ReloadAvatarsCommand>().Add(entityId);
            Pipeline.Run();
        }

        private AvatarLoadState _State(int speakerEntityId) =>
            World.GetPool<AvatarLoadComp>().Read(speakerEntityId).State;

        private int _CountSpeakers()
        {
            var count = 0;
            foreach (var _ in World.Where(out SpeakerAspect _))
                count++;

            return count;
        }

        private int _CountLines()
        {
            var count = 0;
            foreach (var _ in World.Where(out LineAspect _))
                count++;

            return count;
        }

        private sealed class SpeakerAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
        }

        private sealed class LineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
        }
    }
}
