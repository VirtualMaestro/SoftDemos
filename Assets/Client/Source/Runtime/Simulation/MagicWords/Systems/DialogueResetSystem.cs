using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public sealed class DialogueResetSystem :
        IEcsRun,
        IEcsDestroy,
        IEcsInject<EcsWorld>,
        IEcsInject<IDialogueService>,
        IEcsInject<IImageLoadService>,
        IEcsInject<ILog>
    {
        private EcsWorld _world;
        private IDialogueService _dialogueSource;
        private IImageLoadService _imageSource;
        private ILog _log;

        public void Run()
        {
            var hasResetCommand = false;
            foreach (var entityId in _world.Where(out ResetCommandAspect _))
            {
                _world.DelEntity(entityId);
                hasResetCommand = true;
            }

            if (hasResetCommand == false)
                return;

            var hadDialogueRequest = _ReleaseDialogueRequest();
            var openRequestCount = _ReleaseOpenRequests();
            var speakerCount = _DeleteSpeakers();
            var lineCount = _DeleteLines();

            ref var playback = ref _world.Get<DialoguePlaybackComp>();
            var revealedLineCount = playback.VisibleLineCount;
            playback = default;
            ref var state = ref _world.Get<DialogueStateComp>();
            state = default;
            ref var payload = ref _world.Get<DialoguePayloadComp>();
            payload = default;
            _log.Info(
                $"Dialogue reset; dropped {lineCount} line(s), {speakerCount} speaker(s), " +
                $"{revealedLineCount} revealed line(s), " +
                $"{openRequestCount} open avatar request(s), " +
                $"{(hadDialogueRequest ? 1 : 0)} open dialogue request(s).");
        }

        void IEcsDestroy.Destroy()
        {
            _ReleaseDialogueRequest();
            _ReleaseOpenRequests();
        }

        /// <summary>
        /// Releases a dialogue fetch that is still in flight. The happy path releases in
        /// <see cref="DialogueFetchSystem"/> right after <c>Resolve</c>, so only a reset or a
        /// teardown mid-fetch reaches this.
        /// </summary>
        private bool _ReleaseDialogueRequest()
        {
            ref var state = ref _world.Get<DialogueStateComp>();

            if (state.State != DialogueLoadState.Loading || state.RequestId == 0)
                return false;

            _dialogueSource.Release(state.RequestId);
            state.RequestId = 0;
            return true;
        }

        private int _ReleaseOpenRequests()
        {
            var releasedCount = 0;
            foreach (var entityId in _world.Where(out SpeakerAspect aspect))
            {
                ref var load = ref aspect.Loads.Get(entityId);

                if (load.State != AvatarLoadState.Loading &&
                    load.State != AvatarLoadState.Ready)
                {
                    continue;
                }

                _imageSource.Release(load.RequestId);
                load.RequestId = 0;
                load.HandleId = 0;
                load.State = default;
                releasedCount++;
            }

            return releasedCount;
        }

        private int _DeleteSpeakers()
        {
            var count = 0;
            foreach (var entityId in _world.Where(out SpeakerAspect _))
            {
                _world.DelEntity(entityId);
                count++;
            }

            return count;
        }

        private int _DeleteLines()
        {
            var count = 0;
            foreach (var entityId in _world.Where(out LineAspect _))
            {
                _world.DelEntity(entityId);
                count++;
            }

            return count;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(IDialogueService obj) => _dialogueSource = obj;
        public void Inject(IImageLoadService obj) => _imageSource = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class ResetCommandAspect : EcsAspect
        {
            public EcsPool<ResetDialogueCommand> Commands = Inc;
        }

        private sealed class SpeakerAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
            public EcsPool<AvatarLoadComp> Loads = Inc;
        }

        private sealed class LineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
        }
    }
}
