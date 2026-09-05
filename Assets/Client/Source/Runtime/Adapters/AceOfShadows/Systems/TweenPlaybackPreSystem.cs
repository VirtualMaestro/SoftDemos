using Client.Simulation.Core.Phases;
using Client.Adapters.AceOfShadows.Components;
using Client.Adapters.AceOfShadows.Services;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Components.Commands;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>Starts one tween per flight through <see cref="CardMovePlayerService"/>.</summary>
    /// <remarks>
    /// The simulation puts a card in flight with a <c>MovingComp</c> carrying a slot index and a
    /// duration. This system moves the entity's view to that slot and marks the entity with its own
    /// <see cref="TweenRunningTag"/> so it starts one tween per flight.
    /// <para>It does not report the completion. A tween that ends leaves its entity in the player's
    /// queue, and <c>AceOfShadowsInpSystem</c> drains that queue into <c>MoveCompletedCommand</c>
    /// on the next frame's Input — a one-frame component written here would be deleted by this same
    /// frame's Cleanup, unseen by any Sim. The queue is what makes the hand-off legal: the player
    /// never touches the world, and the world is written from one phase body.</para>
    /// <para>It does not stop the tweens on destroy either. The player owns them and kills them
    /// when the composition root disposes it — a system that released on <c>Destroy</c> was a
    /// system that owned (adr-data-placement-is-decided-on-three-axes rule 8).</para>
    /// </remarks>
    internal sealed class TweenPlaybackPreSystem : IEcsInit, IEcsPresent,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<ViewRegistryService>,
        IEcsInject<StackSlotLayoutService>, IEcsInject<CardMovePlayerService>
    {
        private EcsWorld _world;
        private ILogService _log;
        private ViewRegistryService _viewRegistry;
        private StackSlotLayoutService _stackSlotLayout;
        private CardMovePlayerService _tweenPlayer;

        private EcsTagPool<TweenRunningTag> _runningTweens;

        public void Init()
        {
            // The component's marker interface picks the pool type, so a tag gets EcsTagPool.
            _runningTweens = _world.GetPool<TweenRunningTag>();
        }

        public void Present()
        {
            // No views registered means the stage is closed or closing — the input half runs
            // earlier in the frame, so on the teardown frame this cancels before any move could
            // read as failed. The cards in flight are the simulation's and its reset deletes them;
            // the tween marker is this system's, and dropping it is all a cancellation is.
            if (_viewRegistry.Count == 0)
                _ForgetRunningTweens();
            else
                _StartNewTweens();
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

                if (!_viewRegistry.TryResolve(handleId, out var view, out var card))
                {
                    // Report a failure as a completion, through the queue rather than the world:
                    // the simulation must not wait forever, and a *Command written in Present dies
                    // in this frame's Cleanup before any Sim sees it. Input drains it next frame.
                    _log.Error($"Entity {entityId}: view handle #{handleId} does not resolve. " +
                               "Reporting the flight as finished so the simulation can land it.");
                    _tweenPlayer.ReportCompleted(_world.GetEntityLong(entityId));
                    // Marked as running so the report is made once. Input drops the tag with it.
                    _runningTweens.TryAdd(entityId);
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
