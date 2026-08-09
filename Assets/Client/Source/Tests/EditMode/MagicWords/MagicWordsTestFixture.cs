// Captured payload: 17 dialogue lines and 5 avatar entries.

using System.IO;
using DCFApixels.DragonECS;
using Game.Simulation.MagicWords;
using Game.Simulation.Ports;
using Game.Simulation.Tests.Fakes;
using NUnit.Framework;
using UnityEngine;

namespace Game.Simulation.Tests.MagicWords
{
    public abstract class MagicWordsTestFixture
    {
        protected EcsWorld World { get; private set; }
        protected EcsPipeline Pipeline { get; private set; }
        protected FakeLog Log { get; private set; }
        protected FakeDialogueService DialogueSource { get; private set; }
        protected FakeImageService ImageSource { get; private set; }
        protected FakeTime Time { get; private set; }

        [SetUp]
        public void SetUp()
        {
            World = new EcsWorld();
            Log = new FakeLog();
            DialogueSource = new FakeDialogueService();
            ImageSource = new FakeImageService();
            Time = new FakeTime();
            Pipeline = EcsPipeline.New()
                .Inject(World)
                .Inject<ILog>(Log)
                .Inject<IDialogueService>(DialogueSource)
                .Inject<IImageLoadService>(ImageSource)
                .Inject<ITimeService>(Time)
                .AddModule(new MagicWordsModule(CreateConfig()))
                .BuildAndInit();
        }

        [TearDown]
        public void TearDown()
        {
            _DestroyPipeline();

            World?.Destroy();
            World = null;
        }

        protected static DialoguePayload LoadPayload()
        {
            var path = Path.Combine(
                Application.dataPath,
                "Client/Source/Tests/EditMode/MagicWords/Fixtures/magicwords-v3.json");
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<DialoguePayload>(json);
        }

        protected void _Load()
        {
            var entityId = World.NewEntity();
            World.GetPool<LoadDialogueCommand>().Add(entityId);
            Pipeline.Run();
        }

        protected void _Reset()
        {
            var entityId = World.NewEntity();
            World.GetPool<ResetDialogueCommand>().Add(entityId);
            Pipeline.Run();
        }

        protected void _Tick()
        {
            Pipeline.Run();
        }

        protected void _Advance(float seconds)
        {
            Time.DeltaSeconds = seconds;
            try
            {
                Pipeline.Run();
            }
            finally
            {
                Time.DeltaSeconds = 0f;
            }
        }

        protected virtual MagicWordsConfig CreateConfig() => new();

        protected void _DestroyPipeline()
        {
            Pipeline?.Destroy();
            Pipeline = null;
        }
    }
}
