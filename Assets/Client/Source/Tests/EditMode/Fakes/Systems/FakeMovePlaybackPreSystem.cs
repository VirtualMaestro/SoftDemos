using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Components.Commands;
using Client.Simulation.Core.Phases;
using Client.Simulation.Tests.Fakes.Services;
using DCFApixels.DragonECS;

namespace Client.Simulation.Tests.Fakes.Systems
{
    /// <summary>EditMode stand-in for <c>TweenPlaybackPreSystem</c>: starts one flight per move.</summary>
    /// <remarks>
    /// Present, like the system it stands in for, and for the same reason: a flight is started
    /// against a view, after the Sim that issued it, in the same frame. It writes no one-frame
    /// component — <c>FakeMoveCompletionInpSystem</c> does that from Input, because anything written
    /// here would be deleted by this frame's Cleanup before a Sim could read it.
    /// <para>This assembly references no adapter assembly on purpose, so the contract is
    /// re-implemented rather than reused. The split into two systems and a player service is not
    /// decoration: it is what makes the fake's timing the real one's.</para>
    /// </remarks>
    public sealed class FakeMovePlaybackPreSystem : IEcsPresent, IEcsInject<EcsWorld>,
        IEcsInject<FakeMovePlayerService>
    {
        private EcsWorld _world;
        private FakeMovePlayerService _player;

        public void Present()
        {
            foreach (var entityId in _world.Where(out MoveAspect aspect))
            {
                if (_player.IsPending(entityId))
                    continue;

                if (_player.DropAllMoves)
                {
                    // The flight cannot run. Report it as finished through the queue, the way the
                    // real system reports an unresolvable view handle.
                    aspect.Moving.TryDel(entityId);
                    _player.ReportCompleted(_world.GetEntityLong(entityId));
                    continue;
                }

                _player.StartMove(_world.GetEntityLong(entityId));
            }
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(FakeMovePlayerService obj) => _player = obj;

        private sealed class MoveAspect : EcsAspect
        {
            public readonly EcsPool<MovingComp> Moving = Inc;

            // Excluded, not optional: a flight the fake has already reported is finished must not
            // be picked up again before the simulation has landed it.
            public readonly EcsTagPool<MoveCompletedCommand> Completed = Exc;
        }
    }
}
