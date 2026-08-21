using Client.Adapters.AceOfShadows.Components;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Messages;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>Runs the command, tween and completion cycle through <see cref="TweenPlayerService"/>.</summary>
    /// <remarks>
    /// The simulation adds a <see cref="MoveCommand"/> with a slot index and a duration. This
    /// system moves the entity's view to that slot. When the tween ends, the player queues the
    /// entity. The next <see cref="LateRun"/> drains the queue, removes the command and adds
    /// <see cref="MoveCompletedTag"/>. The queue matters: it keeps every world change on one
    /// thread at one point in the frame. DOTween is only used here, through the player.
    /// </remarks>
    public sealed class TweenPlaybackSystem : IEcsInit, IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILog>, IEcsInject<ViewRegistryService>,
        IEcsInject<StackSlotLayoutService>, IEcsInject<TweenPlayerService>
    {
        private EcsWorld _world;
        private ILog _log;
        private ViewRegistryService _viewRegistry;
        private StackSlotLayoutService _stackSlotLayout;
        private TweenPlayerService _tweenPlayer;

        private EcsPool<MoveCommand> _moveCommands;
        private EcsTagPool<MoveCompletedTag> _completedTweens;
        private EcsTagPool<TweenRunningTag> _runningTweens;

        public void Init()
        {
            // The component's marker interface picks the pool type, so a tag gets EcsTagPool.
            _moveCommands = _world.GetPool<MoveCommand>();
            _completedTweens = _world.GetPool<MoveCompletedTag>();
            _runningTweens = _world.GetPool<TweenRunningTag>();
        }

        public void LateRun()
        {
            _HandleCompletedTweens();
            _StartNewTweens();
        }

        public void Destroy()
        {
            _tweenPlayer.KillAll();
        }

        private void _HandleCompletedTweens()
        {
            if (!_tweenPlayer.HasCompletedTweens)
                return;

            foreach (var completion in _tweenPlayer.Completions)
            {
                // The entity can die while its tween runs. entlong holds a generation, so a
                // recycled id reads as dead.
                if (!completion.TryGetID(out var entityId))
                {
                    _log.Warn("A tween completed for an entity that no longer exists. Ignoring.");
                    continue;
                }

                _moveCommands.TryDel(entityId);
                _runningTweens.TryDel(entityId);
                _completedTweens.TryAdd(entityId);
            }

            _tweenPlayer.ClearCompletions();
        }

        private void _StartNewTweens()
        {
            foreach (var entityId in _world.Where(out MoveAspect aspect))
            {
                var handleId = aspect.Views.Read(entityId).Id;

                if (_viewRegistry.TryResolve(handleId, out var view, out var card) == false)
                {
                    // Report a failure as a completion. The simulation must not wait forever.
                    _log.Error($"Entity {entityId}: view handle #{handleId} does not resolve. " +
                               "Dropping the move command.");
                    _moveCommands.TryDel(entityId);
                    _completedTweens.TryAdd(entityId);
                    continue;
                }

                ref readonly var command = ref aspect.Commands.Read(entityId);
                _runningTweens.TryAdd(entityId);
                _tweenPlayer.StartMove(view, card,
                    _stackSlotLayout.SlotPosition(command.TargetSlot, command.TargetDepth),
                    command.Duration, _world.GetEntityLong(entityId));
            }
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
        public void Inject(ViewRegistryService obj) => _viewRegistry = obj;
        public void Inject(StackSlotLayoutService obj) => _stackSlotLayout = obj;
        public void Inject(TweenPlayerService obj) => _tweenPlayer = obj;

        private sealed class MoveAspect : EcsAspect
        {
            public EcsPool<MoveCommand> Commands = Inc;
            public EcsPool<ViewHandleComp> Views = Inc;
            public EcsTagPool<TweenRunningTag> Running = Exc;
            public EcsTagPool<MoveCompletedTag> Completed = Exc;
        }
    }
}
