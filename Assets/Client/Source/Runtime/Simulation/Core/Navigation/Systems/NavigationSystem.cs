using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Navigation.Components;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Navigation.Systems
{
    /// <summary>
    /// Handles open/close demo commands: starts scene load/unload, polls the async operation, and
    /// moves the screen state through Menu → Loading → Demo → Unloading → Menu.
    /// </summary>
    public sealed class NavigationSystem : IEcsSim, IEcsInject<EcsWorld>,
        IEcsInject<ISceneService>, IEcsInject<ILogService>
    {
        private readonly DemoCatalog _catalog;

        private EcsWorld _world;
        private ISceneService _sceneService;
        private ILogService _log;

        public NavigationSystem(DemoCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Sim()
        {
            ref var state = ref _world.Get<ScreenStateComp>();

            _ConsumeOpenCommands(ref state);
            _ConsumeCloseCommands(ref state);
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
                    continue;
                }

                if (command.DemoIndex < 0 || command.DemoIndex >= _catalog.Count)
                {
                    _log.Error($"OpenDemoCommand index {command.DemoIndex} is outside " +
                        $"the demo catalog range [0, {_catalog.Count}).");
                    continue;
                }

                state.LastOperationFailed = false;
                state.ActiveDemoIndex = command.DemoIndex;
                state.PendingRequestId = _sceneService.Request(new SceneLoadRequest(_catalog[command.DemoIndex]));

                _Transition(ref state, ScreenId.Loading);
            }
        }

        private void _ConsumeCloseCommands(ref ScreenStateComp state)
        {
            // The entity id is not read: a close command carries nothing, and the one that deletes
            // it is NavigationCleanupSystem. Iterating still matters — a second command in the same
            // frame is reported, the way a second one always was.
            foreach (var closeEntity in _world.Where(out CloseCommandAspect _))
            {
                if (state.Current != ScreenId.Demo)
                {
                    _log.Warn($"CloseDemoCommand ignored in {state.Current}.");
                    continue;
                }

                state.PendingRequestId = _sceneService.Request(new SceneUnloadRequest(_catalog[state.ActiveDemoIndex]));
                _Transition(ref state, ScreenId.Unloading);
            }
        }

        private void _PollPendingOperation(ref ScreenStateComp state)
        {
            if (state.Current != ScreenId.Loading && state.Current != ScreenId.Unloading)
                return;

            var status = _sceneService.Poll(state.PendingRequestId);

            if (status == AsyncOpStatus.Pending)
                return;

            var requestId = state.PendingRequestId;
            _sceneService.Release(requestId);
            state.PendingRequestId = -1;

            if (state.Current == ScreenId.Loading)
            {
                if (status == AsyncOpStatus.Done)
                {
                    _Transition(ref state, ScreenId.Demo);
                    return;
                }

                state.LastOperationFailed = true;
                _log.Error($"Load request #{requestId} failed for address '{_catalog[state.ActiveDemoIndex]}'.");
                state.ActiveDemoIndex = -1;
                _Transition(ref state, ScreenId.Menu);
                return;
            }

            if (status == AsyncOpStatus.Failed)
            {
                state.LastOperationFailed = true;
                _log.Error($"Unload request #{requestId} failed for address '{_catalog[state.ActiveDemoIndex]}'.");
            }

            state.ActiveDemoIndex = -1;
            _Transition(ref state, ScreenId.Menu);
        }

        private static void _Transition(ref ScreenStateComp state, ScreenId next) =>
            state.Current = next;

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ISceneService obj) => _sceneService = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class OpenCommandAspect : EcsAspect
        {
            public readonly EcsPool<OpenDemoCommand> Commands = Inc;
        }

        private sealed class CloseCommandAspect : EcsAspect
        {
            public readonly EcsPool<CloseDemoCommand> _ = Inc;
        }
    }
}
