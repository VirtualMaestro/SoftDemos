using Client.Adapters.AceOfShadows.Components;
using Client.Adapters.AceOfShadows.Services;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Components;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>Runs the flight, tween and completion cycle through <see cref="CardMovePlayerService"/>.</summary>
    /// <remarks>
    /// The simulation puts a card in flight with a <c>MovingComp</c> carrying a slot index and a
    /// duration. This system moves the entity's view to that slot, marks the entity with its own
    /// <see cref="TweenRunningTag"/> so it starts one tween per flight, and when the tween ends the
    /// player queues the entity. The next <see cref="LateRun"/> drains the queue and adds
    /// <see cref="MoveCompletedCommand"/>, which is the simulation's cue to land the card and drop
    /// the flight. The queue matters: it keeps every world change on one thread at one point in the
    /// frame — the player never touches the world, THIS is the one place that writes them.
    /// </remarks>
    public sealed class TweenPlaybackSystem : IEcsInit, IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<ViewRegistryService>,
        IEcsInject<StackSlotLayoutService>, IEcsInject<CardMovePlayerService>,
        IEcsInject<CardViewChannel>
    {
        private EcsWorld _world;
        private ILogService _log;
        private ViewRegistryService _viewRegistry;
        private StackSlotLayoutService _stackSlotLayout;
        private CardMovePlayerService _tweenPlayer;
        private CardViewChannel _cardViewChannel;

        private EcsTagPool<MoveCompletedCommand> _completedTweens;
        private EcsTagPool<TweenRunningTag> _runningTweens;

        public void Init()
        {
            // The component's marker interface picks the pool type, so a tag gets EcsTagPool.
            _completedTweens = _world.GetPool<MoveCompletedCommand>();
            _runningTweens = _world.GetPool<TweenRunningTag>();
        }

        public void LateRun()
        {
            _HandleCompletedTweens();

            // No views registered means the stage is closed or closing — the stage system runs
            // earlier in LateRun, so on the teardown frame this cancels in the SAME frame the
            // views died, before any move could read as failed. The cards in flight are the
            // simulation's and its reset deletes them; the tween marker is this system's, and
            // dropping it is all a cancellation is.
            if (_cardViewChannel.Handles.Count == 0)
                _ForgetRunningTweens();
            else
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

                _runningTweens.TryDel(entityId);
                _completedTweens.TryAdd(entityId);
            }

            _tweenPlayer.ClearCompletions();
        }

        private void _ForgetRunningTweens()
        {
            // MoveCompletedCommand is deliberately NOT added: a cancelled move never happened, and
            // the simulation is not waiting — it issued the reset that killed the stage.
            foreach (var entityId in _world.Where(out CancelAspect _))
                _runningTweens.TryDel(entityId);
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
                               "Reporting the flight as finished so the simulation can land it.");
                    _completedTweens.TryAdd(entityId);
                    continue;
                }

                ref readonly var moving = ref aspect.Moving.Read(entityId);
                _runningTweens.TryAdd(entityId);
                _tweenPlayer.StartMove(view, card,
                    _stackSlotLayout.SlotPosition(moving.TargetStack, moving.TargetOrder),
                    moving.DurationSeconds, _world.GetEntityLong(entityId));
            }
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(ViewRegistryService obj) => _viewRegistry = obj;
        public void Inject(StackSlotLayoutService obj) => _stackSlotLayout = obj;
        public void Inject(CardMovePlayerService obj) => _tweenPlayer = obj;
        public void Inject(CardViewChannel obj) => _cardViewChannel = obj;

        private sealed class MoveAspect : EcsAspect
        {
            public readonly EcsPool<MovingComp> Moving = Inc;
            public readonly EcsPool<ViewHandleComp> Views = Inc;
            public readonly EcsTagPool<TweenRunningTag> Running = Exc;
            public readonly EcsTagPool<MoveCompletedCommand> Completed = Exc;
        }

        private sealed class CancelAspect : EcsAspect
        {
            public readonly EcsPool<ViewHandleComp> Views = Inc;
        }
    }
}
