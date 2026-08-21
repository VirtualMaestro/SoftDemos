using System.Collections;
using System.Collections.Generic;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.Shared.Services;
using Client.Simulation.Shared.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    public sealed class MagicWordsIntegrationTests
    {
        private const float TimeoutSeconds = 20f;

        private EcsWorld _world;
        private EcsPipeline _pipeline;
        private HttpDialogueService _dialogueSource;
        private WebImageLoaderService _imageSource;

        [SetUp]
        public void SetUp()
        {
            _world = new EcsWorld();
            _dialogueSource = new HttpDialogueService(new UnityLogService("Test.Dialogue"));
            _imageSource = new WebImageLoaderService(new UnityLogService("Test.Avatars"));
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ILogService>(new UnityLogService("Test.MagicWords"))
                .Inject<IDialogueService>(_dialogueSource)
                .Inject<IImageLoadService>(_imageSource)
                .Inject<ITimeService>(new UnityTimeService())
                .AddModule(new MagicWordsModule(new MagicWordsConfig()))
                .BuildAndInit();
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline?.Destroy();
            _pipeline = null;

            _dialogueSource?.Dispose();
            _dialogueSource = null;

            _imageSource?.Dispose();
            _imageSource = null;

            _world?.Destroy();
            _world = null;
        }

        [UnityTest]
        [Category("Network")]
        public IEnumerator RealAdapters_LoadDialogueAndAvatar_ThenResetCleanly()
        {
            _IgnoreIfOffline();

            var commandEntityId = _world.NewEntity();
            _world.GetPool<LoadDialogueCommand>().Add(commandEntityId);
            yield return _TickUntilDialogueReady();

            var sheldonId = _AssertDialogueReady();
            _world.GetPool<RequestAvatarCommand>().Add(sheldonId);
            yield return _TickUntilAvatarSettled(sheldonId);

            _AssertAvatarReady(sheldonId);

            commandEntityId = _world.NewEntity();
            _world.GetPool<ResetDialogueCommand>().Add(commandEntityId);
            _pipeline.Run();

            Assert.That(_CountLines(), Is.Zero);
            Assert.That(_CountSpeakers(), Is.Zero);
            Assert.That(_world.Get<DialogueStateComp>(), Is.EqualTo(default(DialogueStateComp)));
            Assert.That(_dialogueSource.OpenRequestCount, Is.Zero);
            Assert.That(_imageSource.OpenRequestCount, Is.Zero);
            Assert.That(_imageSource.HeldTextureCount, Is.Zero);
        }

        private int _AssertDialogueReady()
        {
            var lineIndices = new List<int>();
            foreach (var entityId in _world.Where(out LineAspect lineAspect))
                lineIndices.Add(lineAspect.Lines.Read(entityId).Index);

            Assert.That(lineIndices, Is.EqualTo(new[]
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            }));

            var speakers = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var entityId in _world.Where(out SpeakerAspect speakerAspect))
                speakers.Add(speakerAspect.Speakers.Read(entityId).Name, entityId);

            Assert.That(speakers.Keys,
                Is.EquivalentTo(new[] { "Sheldon", "Leonard", "Penny", "Neighbour" }));
            Assert.That(speakers, Has.Count.EqualTo(4));
            Assert.That(
                _world.GetPool<AvatarLoadComp>().Read(speakers["Neighbour"]).State,
                Is.EqualTo(AvatarLoadState.Missing));
            Assert.That(_dialogueSource.OpenRequestCount, Is.Zero);

            return speakers["Sheldon"];
        }

        private void _AssertAvatarReady(int sheldonId)
        {
            var avatarLoad = _world.GetPool<AvatarLoadComp>().Read(sheldonId);
            Assert.That(avatarLoad.State, Is.EqualTo(AvatarLoadState.Ready));
            Assert.That(avatarLoad.HandleId, Is.Not.Zero);
            Assert.That(_imageSource.TryGetTexture(avatarLoad.HandleId, out var texture), Is.True);
            Assert.That(texture, Is.Not.Null);
        }

        private IEnumerator _TickUntilDialogueReady()
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_world.Get<DialogueStateComp>().State != DialogueLoadState.Ready)
            {
                _pipeline.Run();
                Assert.That(_world.Get<DialogueStateComp>().State, Is.Not.EqualTo(DialogueLoadState.Failed));
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Dialogue did not reach Ready within {TimeoutSeconds}s.");
                yield return null;
            }
        }

        private IEnumerator _TickUntilAvatarSettled(int speakerEntityId)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            var loads = _world.GetPool<AvatarLoadComp>();

            while (loads.Read(speakerEntityId).State is AvatarLoadState.NotRequested or AvatarLoadState.Loading)
            {
                _pipeline.Run();
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Avatar did not settle within {TimeoutSeconds}s.");
                yield return null;
            }
        }

        private int _CountSpeakers()
        {
            var count = 0;
            foreach (var _ in _world.Where(out SpeakerAspect _))
                count++;

            return count;
        }

        private int _CountLines()
        {
            var count = 0;
            foreach (var _ in _world.Where(out LineAspect _))
                count++;

            return count;
        }

        private static void _IgnoreIfOffline()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("Network test skipped because Unity reports no internet connection.");
        }

        private sealed class LineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
        }

        private sealed class SpeakerAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
        }
    }
}
