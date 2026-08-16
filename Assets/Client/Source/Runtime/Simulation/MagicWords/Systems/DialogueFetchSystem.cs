using System.Collections.Generic;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public sealed class DialogueFetchSystem :
        IEcsRun,
        IEcsInject<EcsWorld>,
        IEcsInject<IDialogueService>,
        IEcsInject<ILog>
    {
        private readonly List<int> _entitiesToDelete = new();

        private EcsWorld _world;
        private IDialogueService _dialogueSource;
        private ILog _log;

        public void Run()
        {
            ref var state = ref _world.Get<DialogueStateComp>();

            foreach (var entityId in _world.Where(out LoadCommandAspect _))
            {
                if (state.State == DialogueLoadState.Loading ||
                    state.State == DialogueLoadState.Ready)
                {
                    _log.Warn($"LoadDialogueCommand ignored while dialogue state is {state.State}.");
                }
                else
                {
                    state.RequestId = _dialogueSource.BeginLoad();
                    state.State = DialogueLoadState.Loading;
                    _log.Info("Dialogue fetch started.");
                }

                _entitiesToDelete.Add(entityId);
            }
            _DeleteCollectedEntities();

            if (state.State != DialogueLoadState.Loading || state.RequestId == 0)
                return;

            var requestId = state.RequestId;
            var status = _dialogueSource.Poll(requestId);

            if (status == AsyncOpStatus.Done)
            {
                var payload = _dialogueSource.Resolve(requestId);
                _dialogueSource.Release(requestId);
                state.RequestId = 0;

                if (payload == null)
                {
                    state.State = DialogueLoadState.Failed;
                    _log.Error($"Dialogue fetch request {requestId} completed without a payload.");
                    return;
                }

                ref var payloadComp = ref _world.Get<DialoguePayloadComp>();
                payloadComp.Payload = payload;
                return;
            }

            if (status != AsyncOpStatus.Failed)
                return;

            state.State = DialogueLoadState.Failed;
            state.RequestId = 0;
            _dialogueSource.Release(requestId);
            _log.Error($"Dialogue fetch request {requestId} failed.");
        }

        private void _DeleteCollectedEntities()
        {
            foreach (var entityId in _entitiesToDelete)
                _world.DelEntity(entityId);

            _entitiesToDelete.Clear();
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(IDialogueService obj) => _dialogueSource = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class LoadCommandAspect : EcsAspect
        {
            public EcsPool<LoadDialogueCommand> Commands = Inc;
        }
    }
}
