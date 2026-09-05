using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Components;
using Client.Simulation.MagicWords.Components.Commands;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Systems
{
    /// <summary>
    /// Consumes the reset command: releases open dialogue/image requests, deletes all speaker and
    /// line entities, and zeroes the dialogue state.
    /// </summary>
    /// <remarks>
    /// It releases on the command and never on destroy. Both ports it talks to are disposed by the
    /// composition root, which is where a release right sits
    /// (adr-data-placement-is-decided-on-three-axes rule 8).
    /// </remarks>
    internal sealed class DialogueResetSimSystem :
        IEcsSim,
        IEcsInject<EcsWorld>,
        IEcsInject<IDialogueService>,
        IEcsInject<IImageLoadService>
    {
        private EcsWorld _world;
        private IDialogueService _dialogueSource;
        private IImageLoadService _imageSource;
        private EcsPool<ResetDialogueCommand> _resetCommands;

        public void Sim()
        {
            // Reads the command and never deletes it: DialogueClnSystem owns its one frame.
            if (_resetCommands.Count == 0)
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
            // The payload event needs no wipe here: it lives one frame and cleanup ends it, so a
            // reset cannot leave a stale payload behind for anyone to ingest.
        }

        /// <summary>
        /// Releases a dialogue fetch that is still in flight. The happy path releases in
        /// <see cref="DialogueFetchSimSystem"/> right after <c>Resolve</c>, so only a reset or a
        /// teardown mid-fetch reaches this.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    continue;

                _imageSource.Release(load.RequestId);
                load.RequestId = 0;
                load.State = default;
            }
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _resetCommands = obj.GetPool<ResetDialogueCommand>();
        }

        public void Inject(IDialogueService obj) => _dialogueSource = obj;
        public void Inject(IImageLoadService obj) => _imageSource = obj;

        private sealed class SpeakerAspect : EcsAspect
        {
            public readonly EcsPool<SpeakerComp> Speakers = Inc;
            public readonly EcsPool<AvatarLoadComp> Loads = Inc;
        }

        private sealed class LineAspect : EcsAspect
        {
            public readonly EcsPool<DialogueLineComp> Lines = Inc;
        }
    }
}
