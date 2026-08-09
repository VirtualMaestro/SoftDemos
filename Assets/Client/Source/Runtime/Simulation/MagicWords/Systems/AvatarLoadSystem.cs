using System.Collections.Generic;
using DCFApixels.DragonECS;
using Game.Simulation.Ports;

namespace Game.Simulation.MagicWords
{
    public sealed class AvatarLoadSystem :
        IEcsRun,
        IEcsInject<EcsWorld>,
        IEcsInject<IImageLoadService>,
        IEcsInject<ILog>
    {
        private readonly List<int> _commandsToDelete = new();

        private EcsWorld _world;
        private IImageLoadService _imageSource;
        private ILog _log;
        private EcsPool<ReloadAvatarsCommand> _reloads;
        private EcsPool<RequestAvatarCommand> _requests;
        private EcsPool<AvatarComp> _avatars;

        public void Run()
        {
            var reload = false;
            foreach (var entityId in _world.Where(out ReloadCommandAspect _))
            {
                _commandsToDelete.Add(entityId);
                reload = true;
            }

            foreach (var entityId in _commandsToDelete)
                _reloads.Del(entityId);
            _commandsToDelete.Clear();

            if (reload)
            {
                var reloadCount = 0;
                foreach (var entityId in _world.Where(out ReloadSpeakerAspect aspect))
                {
                    ref var load = ref aspect.Loads.Get(entityId);

                    if (load.State == AvatarLoadState.NotRequested)
                        continue;

                    _imageSource.Release(load.RequestId);
                    load.RequestId = 0;
                    load.HandleId = 0;
                    load.State = AvatarLoadState.NotRequested;
                    _requests.Add(entityId);
                    reloadCount++;
                }

                _log.Info($"Reloading avatars for {reloadCount} speaker(s).");
            }

            foreach (var entityId in _world.Where(out RequestAspect aspect))
            {
                ref var load = ref aspect.Loads.Get(entityId);

                if (load.State == AvatarLoadState.NotRequested)
                {
                    ref readonly var speaker = ref aspect.Speakers.Read(entityId);
                    ref readonly var avatar = ref _avatars.Read(entityId);
                    load.RequestId = _imageSource.BeginLoad(speaker.Name, avatar.Url);
                    load.State = AvatarLoadState.Loading;
                }

                _commandsToDelete.Add(entityId);
            }

            foreach (var entityId in _commandsToDelete)
                _requests.Del(entityId);
            _commandsToDelete.Clear();

            foreach (var entityId in _world.Where(out LoadingAspect aspect))
            {
                ref var load = ref aspect.Loads.Get(entityId);

                if (load.State != AvatarLoadState.Loading)
                    continue;

                var requestId = load.RequestId;
                var status = _imageSource.Poll(requestId);

                if (status == AsyncOpStatus.Done)
                {
                    var handleId = _imageSource.ResolveHandle(requestId);

                    if (handleId == 0)
                    {
                        _Fail(entityId, ref load, requestId, "completed without an image handle");
                        continue;
                    }

                    load.HandleId = handleId;
                    load.State = AvatarLoadState.Ready;
                    ref readonly var speaker = ref aspect.Speakers.Read(entityId);
                    _log.Info($"Avatar for '{speaker.Name}' is ready.");
                    continue;
                }

                if (status == AsyncOpStatus.Failed)
                    _Fail(entityId, ref load, requestId, "failed");
            }
        }

        private void _Fail(int entityId, ref AvatarLoadComp load, int requestId, string reason)
        {
            ref readonly var speaker = ref _world.GetPool<SpeakerComp>().Read(entityId);
            ref readonly var avatar = ref _avatars.Read(entityId);
            load.State = AvatarLoadState.Failed;
            load.RequestId = 0;
            load.HandleId = 0;
            _imageSource.Release(requestId);
            _log.Error(
                $"Avatar request {requestId} for '{speaker.Name}' ({avatar.Url}) {reason}.");
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _reloads = obj.GetPool<ReloadAvatarsCommand>();
            _requests = obj.GetPool<RequestAvatarCommand>();
            _avatars = obj.GetPool<AvatarComp>();
        }

        public void Inject(IImageLoadService obj) => _imageSource = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class ReloadCommandAspect : EcsAspect
        {
            public EcsPool<ReloadAvatarsCommand> Commands = Inc;
        }

        private sealed class ReloadSpeakerAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
            public EcsPool<AvatarComp> Avatars = Inc;
            public EcsPool<AvatarLoadComp> Loads = Inc;
        }

        private sealed class RequestAspect : EcsAspect
        {
            public EcsPool<RequestAvatarCommand> Requests = Inc;
            public EcsPool<SpeakerComp> Speakers = Inc;
            public EcsPool<AvatarLoadComp> Loads = Inc;
        }

        private sealed class LoadingAspect : EcsAspect
        {
            public EcsPool<SpeakerComp> Speakers = Inc;
            public EcsPool<AvatarComp> Avatars = Inc;
            public EcsPool<AvatarLoadComp> Loads = Inc;
        }
    }
}
