using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Ports.Requests;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Systems
{
    /// <summary>
    /// Handles avatar image requests: starts a download per request command, polls running ones,
    /// stores the image handle or a Failed state; on reload releases everything and re-requests.
    /// </summary>
    internal sealed class AvatarLoadSystem :
        IEcsSim,
        IEcsInject<EcsWorld>,
        IEcsInject<IImageLoadService>,
        IEcsInject<ILogService>
    {
        private EcsWorld _world;
        private IImageLoadService _imageSource;
        private ILogService _log;
        private EcsPool<ReloadAvatarsCommand> _reloads;
        private EcsPool<RequestAvatarCommand> _requests;
        private EcsPool<AvatarComp> _avatars;

        public void Sim()
        {
            // Both commands below are read and never deleted: DialogueCleanupSystem owns the one
            // frame they live, so a reload asked for this frame cannot be seen again on the next.
            var reload = _reloads.Count > 0;

            if (reload)
            {
                foreach (var entityId in _world.Where(out SpeakerAspect aspect))
                {
                    ref var load = ref aspect.Loads.Get(entityId);

                    if (load.State == AvatarLoadState.NotRequested)
                        continue;

                    _imageSource.Release(load.RequestId);
                    load.RequestId = 0;
                    load.State = AvatarLoadState.NotRequested;

                    // A request from this frame's playback sits on a NotRequested speaker, which
                    // the guard above already skipped — so this never doubles. The check is the
                    // cheap half of not depending on that reading staying true.
                    if (!_requests.Has(entityId))
                        _requests.Add(entityId);
                }

                _log.Info("Reloading avatars.");
            }

            foreach (var entityId in _world.Where(out RequestAspect aspect))
            {
                ref var load = ref aspect.Loads.Get(entityId);

                if (load.State == AvatarLoadState.NotRequested)
                {
                    ref readonly var speaker = ref aspect.Speakers.Read(entityId);
                    ref readonly var avatar = ref _avatars.Read(entityId);
                    load.RequestId = _imageSource.Request(new ImageLoadRequest(speaker.Name, avatar.Url));
                    load.State = AvatarLoadState.Loading;
                }
            }

            foreach (var entityId in _world.Where(out SpeakerAspect aspect))
            {
                ref var load = ref aspect.Loads.Get(entityId);

                if (load.State != AvatarLoadState.Loading)
                    continue;

                var requestId = load.RequestId;
                var status = _imageSource.Poll(requestId);

                if (status == AsyncOpStatus.Done)
                {
                    load.State = AvatarLoadState.Ready;
                    _log.Info($"Avatar for '{aspect.Speakers.Read(entityId).Name}' is ready.");
                    continue;
                }

                if (status == AsyncOpStatus.Failed)
                    _Fail(entityId, ref load, requestId, "failed");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _Fail(int entityId, ref AvatarLoadComp load, int requestId, string reason)
        {
            load.State = AvatarLoadState.Failed;
            load.RequestId = 0;
            _imageSource.Release(requestId);

            _log.Error(
                $"Avatar request {requestId} for '{_world.GetPool<SpeakerComp>().Read(entityId).Name}' " +
                $"({_avatars.Read(entityId).Url}) {reason}.");
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _reloads = obj.GetPool<ReloadAvatarsCommand>();
            _requests = obj.GetPool<RequestAvatarCommand>();
            _avatars = obj.GetPool<AvatarComp>();
        }

        public void Inject(IImageLoadService obj) => _imageSource = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class RequestAspect : EcsAspect
        {
            public readonly EcsPool<RequestAvatarCommand> _ = Inc;
            public readonly EcsPool<SpeakerComp> Speakers = Inc;
            public readonly EcsPool<AvatarLoadComp> Loads = Inc;
        }

        private sealed class SpeakerAspect : EcsAspect
        {
            public readonly EcsPool<SpeakerComp> Speakers = Inc;
            public readonly EcsPool<AvatarComp> _ = Inc;
            public readonly EcsPool<AvatarLoadComp> Loads = Inc;
        }
    }
}
