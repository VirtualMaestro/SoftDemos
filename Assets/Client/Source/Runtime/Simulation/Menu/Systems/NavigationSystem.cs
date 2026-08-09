using System.Collections.Generic;
using DCFApixels.DragonECS;
using Game.Simulation.Ports;

namespace Game.Simulation.Menu
{
    public sealed class NavigationSystem : IEcsRun, IEcsInject<EcsWorld>,
        IEcsInject<ISceneService>, IEcsInject<ILog>
    {
        private readonly DemoCatalog _catalog;
        private readonly List<int> _consumedCommands = new();

        private EcsWorld _world;
        private ISceneService _scenes;
        private ILog _log;

        public NavigationSystem(DemoCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Run()
        {
            ref var state = ref _world.Get<ScreenStateComp>();

            _ConsumeOpenCommands(ref state);
            _DeleteConsumedCommands();

            _ConsumeCloseCommands(ref state);
            _DeleteConsumedCommands();

            _PollPendingOperation(ref state);
        }

        private void _ConsumeOpenCommands(ref ScreenStateComp state)
        {
            foreach (var entityId in _world.Where(out OpenCommandAspect aspect))
            {
                ref readonly var command = ref aspect.Commands.Read(entityId);

                if (state.Current != ScreenId.Menu)
                {
                    _log.Warn($"OpenDemoCommand({command.DemoIndex}) ignored in {state.Current}.");
                    _consumedCommands.Add(entityId);
                    continue;
                }

                if (command.DemoIndex < 0 || command.DemoIndex >= _catalog.Count)
                {
                    _log.Error($"OpenDemoCommand index {command.DemoIndex} is outside " +
                        $"the demo catalog range [0, {_catalog.Count}).");
                    _consumedCommands.Add(entityId);
                    continue;
                }

                var address = _catalog[command.DemoIndex];
                state.LastOperationFailed = false;
                state.ActiveDemoIndex = command.DemoIndex;
                state.PendingRequestId = _scenes.BeginLoad(address);

                _Transition(ref state, ScreenId.Loading, address);

                _consumedCommands.Add(entityId);
            }
        }

        private void _ConsumeCloseCommands(ref ScreenStateComp state)
        {
            foreach (var entityId in _world.Where(out CloseCommandAspect _))
            {
                if (state.Current != ScreenId.Demo)
                {
                    _log.Warn($"CloseDemoCommand ignored in {state.Current}.");
                    _consumedCommands.Add(entityId);
                    continue;
                }

                var address = _catalog[state.ActiveDemoIndex];
                state.PendingRequestId = _scenes.BeginUnload(address);
                _Transition(ref state, ScreenId.Unloading, address);
                _consumedCommands.Add(entityId);
            }
        }

        private void _PollPendingOperation(ref ScreenStateComp state)
        {
            if (state.Current != ScreenId.Loading && state.Current != ScreenId.Unloading)
                return;

            var status = _scenes.Poll(state.PendingRequestId);

            if (status == AsyncOpStatus.Pending)
                return;

            var requestId = state.PendingRequestId;
            var address = _catalog[state.ActiveDemoIndex];
            _scenes.Release(requestId);
            state.PendingRequestId = -1;

            if (state.Current == ScreenId.Loading)
            {
                if (status == AsyncOpStatus.Done)
                {
                    _Transition(ref state, ScreenId.Demo, address, requestId);
                    return;
                }

                state.LastOperationFailed = true;
                state.ActiveDemoIndex = -1;
                _log.Error($"Load request #{requestId} failed for address '{address}'.");
                _Transition(ref state, ScreenId.Menu, address, requestId);
                return;
            }

            if (status == AsyncOpStatus.Failed)
            {
                state.LastOperationFailed = true;
                _log.Error($"Unload request #{requestId} failed for address '{address}'.");
            }

            state.ActiveDemoIndex = -1;
            _Transition(ref state, ScreenId.Menu, address, requestId);
        }

        private void _Transition(
            ref ScreenStateComp state,
            ScreenId next,
            string address,
            int completedRequestId = -1)
        {
            var previous = state.Current;
            var requestId = completedRequestId >= 0 ? completedRequestId : state.PendingRequestId;
            state.Current = next;

            _log.Info($"Navigation {previous} -> {next}; demo={state.ActiveDemoIndex}, " +
                $"address='{address}', request=#{requestId}.");
        }

        private void _DeleteConsumedCommands()
        {
            foreach (var entityId in _consumedCommands)
                _world.DelEntity(entityId);

            _consumedCommands.Clear();
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ISceneService obj) => _scenes = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class OpenCommandAspect : EcsAspect
        {
            public EcsPool<OpenDemoCommand> Commands = Inc;
        }

        private sealed class CloseCommandAspect : EcsAspect
        {
            public EcsPool<CloseDemoCommand> Commands = Inc;
        }
    }
}
