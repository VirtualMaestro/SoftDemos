using System.Collections.Generic;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;
using Game.Simulation.Ports;

namespace Game.Simulation.MagicWords
{
    public sealed class DialogueResetSystem :
        IEcsRun,
        IEcsDestroy,
        IEcsInject<EcsWorld>,
        IEcsInject<IDialogueService>,
        IEcsInject<IImageLoadService>,
        IEcsInject<ILog>
    {
        private readonly List<int> _entitiesToDelete = new();

        private EcsWorld _world;
        private IDialogueService _dialogueSource;
        private IImageLoadService _imageSource;
        private ILog _log;

        public void Run()
        {
            var hasResetCommand = false;
            foreach (var entityId in _world.Where(out ResetCommandAspect _))
            {
                _entitiesToDelete.Add(entityId);
                hasResetCommand = true;
            }

            if (hasResetCommand == false)
                return;

            _DeleteCollectedEntities();
            var hadDialogueRequest = _ReleaseDialogueRequest();
            var openRequestCount = _ReleaseOpenRequests();
            var speakerCount = _CollectSpeakers();
            var lineCount = _CollectLines();
            _DeleteCollectedEntities();

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

        private int _CollectSpeakers()
        {
            var count = 0;
            foreach (var entityId in _world.Where(out SpeakerAspect _))
            {
                _entitiesToDelete.Add(entityId);
                count++;
            }

            return count;
        }

        private int _CollectLines()
        {
            var count = 0;
            foreach (var entityId in _world.Where(out LineAspect _))
            {
                _entitiesToDelete.Add(entityId);
                count++;
            }

            return count;
        }

        private void _DeleteCollectedEntities()
        {
            foreach (var entityId in _entitiesToDelete)
                _world.DelEntity(entityId);

            _entitiesToDelete.Clear();
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
