using DCFApixels.DragonECS;
using Game.Simulation.Messages;
using Game.Simulation.Ports;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// The command → tween → completion round-trip, driven through <see cref="TweenPlayer"/> —
    /// the one place DOTween is spoken to.
    ///
    /// The simulation adds a <see cref="MoveCommand"/> carrying a slot index and a duration.
    /// This system resolves the entity's view and asks the player to move it to whatever position
    /// the layout puts that slot at; when the tween finishes the player <b>enqueues</b> the
    /// entity. The queue is drained at the start of the next <see cref="LateRun"/>, where
    /// <see cref="MoveCommand"/> is removed and <see cref="MoveCompletedTag"/> added.
    ///
    /// Draining on the tick rather than inside the DOTween callback is the load-bearing part: it
    /// keeps every world mutation on one thread and at one well-defined point in the frame.
    ///
    /// <see cref="IEcsLateRun"/> comes from the DragonECS-<b>Unity</b> assembly
    /// (<c>src/Buildin/UnityGameCyclieProcesses.cs:21</c>). Core DragonECS only offers
    /// <see cref="IEcsRun"/>, which ticks in <c>Update</c> and would break the rule that views
    /// read simulation state in <c>LateUpdate</c>.
    ///
    /// If DOTween ever proves awkward, replace <see cref="TweenPlayer.StartMove"/> with a
    /// per-tick interpolation: <see cref="MoveCommand"/>, <see cref="MoveCompletedTag"/> and
    /// every test written against them stay unchanged.
    /// </summary>
    public sealed class TweenPlaybackSystem : IEcsInit, IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILog>, IEcsInject<ViewRegistry>
    {
        private readonly StackSlotLayout _layout;
        private readonly TweenPlayer _player;

        private EcsWorld _world;
        private ILog _log;
        private ViewRegistry _views;

        private EcsPool<MoveCommand> _commands;
        private EcsTagPool<MoveCompletedTag> _completed;
        private EcsTagPool<TweenRunningTag> _running;

        public TweenPlaybackSystem(StackSlotLayout layout, TweenPlayer player)
        {
            _layout = layout;
            _player = player;
        }

        public void Init()
        {
            // The GetPool<T> overloads are constrained by the component's marker interface, so a
            // tag component resolves to EcsTagPool<T> without naming the pool type here.
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
                // The entity may have been deleted while its tween was running. entlong carries a
                // generation tag, so a recycled id reads as dead instead of hitting a stranger.
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
                    // Failure is data, not an exception: drop the command and report completion so
                    // the simulation is never left waiting on a move that can never happen.
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
        public void Inject(ViewRegistry obj) => _views = obj;

        private sealed class MoveAspect : EcsAspect
        {
            public EcsPool<MoveCommand> Commands = Inc;
            public EcsPool<ViewHandleComp> Views = Inc;
            public EcsTagPool<TweenRunningTag> Running = Exc;
            public EcsTagPool<MoveCompletedTag> Completed = Exc;
        }
    }
}
