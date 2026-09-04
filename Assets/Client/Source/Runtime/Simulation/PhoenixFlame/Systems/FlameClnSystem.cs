using Client.Simulation.Core.Phases;
using Client.Simulation.PhoenixFlame.Components.Commands;
using DCFApixels.DragonECS;

namespace Client.Simulation.PhoenixFlame.Systems
{
    /// <summary>Deletes the one-frame components the flame demo's simulation owns.</summary>
    /// <remarks>
    /// All three are their own entity. The advance command is the one that makes the rule visible:
    /// <see cref="FlamePhaseRequestSimSystem"/> reads it and ignores it while a transition runs, and
    /// it dies here anyway — so a held button cannot queue a backlog, and the reason is the suffix
    /// rather than a guard reaching a delete.
    /// </remarks>
    internal sealed class FlameClnSystem : IEcsCleanup, IEcsInject<EcsWorld>
    {
        private EcsWorld _world;

        public void Cleanup()
        {
            foreach (var entityId in _world.Where(out SingleAspect<StartFlameCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<ResetFlameCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<AdvanceFlamePhaseCommand> _))
                _world.DelEntity(entityId);
        }

        public void Inject(EcsWorld obj) => _world = obj;
    }
}
