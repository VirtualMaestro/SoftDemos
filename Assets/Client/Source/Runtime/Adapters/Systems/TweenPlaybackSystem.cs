using Client.Adapters.Components;
using Client.Adapters.Services;
using Client.Simulation.Messages;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Adapters.Systems
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
        private ViewRegistryService _views;
        private StackSlotLayoutService _layout;
        private TweenPlayerService _player;

        private EcsPool<MoveCommand> _commands;
        private EcsTagPool<MoveCompletedTag> _completed;
        private EcsTagPool<TweenRunningTag> _running;

        public void Init()
        {
            // The component's marker interface picks the pool type, so a tag gets EcsTagPool.
            _commands = _world.GetPool<MoveCommand>();
            _completed = _world.GetPool<MoveCompletedTag>();
            _running = _world.GetPool<TweenRunningTag>();
        }

        public void LateRun()
        {
            _ApplyCompletions();
            _StartNewTweens();
        }

        public void Destroy()
        {
            _player.KillAll();
        }

        private void _ApplyCompletions()
        {
            if (_player.PendingCompletionCount == 0)
                return;

            foreach (var completion in _player.Completions)
            {
                // The entity can die while its tween runs. entlong holds a generation, so a
                // recycled id reads as dead.
                if (completion.TryGetID(out var entityId) == false)
                {
                    _log.Warn("A tween completed for an entity that no longer exists. Ignoring.");
                    continue;
                }

                _commands.TryDel(entityId);
                _running.TryDel(entityId);
                _completed.TryAdd(entityId);
            }

            _player.ClearCompletions();
        }

        private void _StartNewTweens()
        {
            foreach (var entityId in _world.Where(out MoveAspect aspect))
            {
                var handleId = aspect.Views.Read(entityId).Id;

                if (_views.TryResolve(handleId, out var view, out var card) == false)
                {
                    // Report a failure as a completion. The simulation must not wait forever.
                    _log.Error($"Entity {entityId}: view handle #{handleId} does not resolve. " +
                               "Dropping the move command.");
                    _commands.TryDel(entityId);
                    _completed.TryAdd(entityId);
                    continue;
                }

                ref readonly var command = ref aspect.Commands.Read(entityId);
                _running.TryAdd(entityId);
                _player.StartMove(view, card,
                    _layout.SlotPosition(command.TargetSlot, command.TargetDepth),
                    command.Duration, _world.GetEntityLong(entityId));
            }
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
        public void Inject(ViewRegistryService obj) => _views = obj;
        public void Inject(StackSlotLayoutService obj) => _layout = obj;
        public void Inject(TweenPlayerService obj) => _player = obj;

        private sealed class MoveAspect : EcsAspect
        {
            public EcsPool<MoveCommand> Commands = Inc;
            public EcsPool<ViewHandleComp> Views = Inc;
            public EcsTagPool<TweenRunningTag> Running = Exc;
            public EcsTagPool<MoveCompletedTag> Completed = Exc;
        }
    }
}
