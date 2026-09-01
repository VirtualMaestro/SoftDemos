using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Ports;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Systems
{
    /// <summary>
    /// Consumes the load command, starts the dialogue HTTP request, polls it, and stores the raw
    /// payload (or a Failed state) when it completes.
    /// </summary>
    internal sealed class DialogueFetchSimSystem :
        IEcsSim,
        IEcsInject<EcsWorld>,
        IEcsInject<IDialogueService>,
        IEcsInject<ILogService>
    {
        private EcsWorld _world;
        private IDialogueService _dialogueSource;
        private ILogService _log;
        private EcsPool<DialoguePayloadEvent> _payloads;

        public void Sim()
        {
            ref var state = ref _world.Get<DialogueStateComp>();

            foreach (var _ in _world.Where(out LoadCommandAspect _))
                if (state.State == DialogueLoadState.Loading ||
                    state.State == DialogueLoadState.Ready)
                    _log.Warn($"LoadDialogueCommand ignored while dialogue state is {state.State}.");
                else
                {
                    state.RequestId = _dialogueSource.Request();
                    state.State = DialogueLoadState.Loading;
                }

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

                _payloads.Add(_world.NewEntity()).Payload = payload;
                return;
            }

            if (status != AsyncOpStatus.Failed)
                return;

            state.State = DialogueLoadState.Failed;
            state.RequestId = 0;
            _dialogueSource.Release(requestId);
            _log.Error($"Dialogue fetch request {requestId} failed.");
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _payloads = obj.GetPool<DialoguePayloadEvent>();
        }

        public void Inject(IDialogueService obj) => _dialogueSource = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class LoadCommandAspect : EcsAspect
        {
            public readonly EcsPool<LoadDialogueCommand> Commands = Inc;
        }
    }
}
