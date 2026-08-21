using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Systems
{
    /// <summary>
    /// Consumes the reset command: releases open dialogue/image requests, deletes all speaker and
    /// line entities, and zeroes the dialogue state.
    /// </summary>
    public sealed class DialogueResetSystem :
        IEcsRun,
        IEcsDestroy,
        IEcsInject<EcsWorld>,
        IEcsInject<IDialogueService>,
        IEcsInject<IImageLoadService>
    {
        private EcsWorld _world;
        private IDialogueService _dialogueSource;
        private IImageLoadService _imageSource;

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

            _ReleaseDialogueRequest();
            _ReleaseOpenRequests();

            foreach (var entityId in _world.Where(out SpeakerAspect _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out LineAspect _))
                _world.DelEntity(entityId);

            ref var playback = ref _world.Get<DialoguePlaybackComp>();
            playback = default;
            ref var state = ref _world.Get<DialogueStateComp>();
            state = default;
            ref var payload = ref _world.Get<DialoguePayloadComp>();
            payload = default;
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
        private void _ReleaseDialogueRequest()
        {
            ref var state = ref _world.Get<DialogueStateComp>();

            if (state.State != DialogueLoadState.Loading || state.RequestId == 0)
                return;

            _dialogueSource.Release(state.RequestId);
            state.RequestId = 0;
        }

        private void _ReleaseOpenRequests()
        {
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
            }
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(IDialogueService obj) => _dialogueSource = obj;
        public void Inject(IImageLoadService obj) => _imageSource = obj;

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
