using Client.Simulation.Core.Phases;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Systems
{
    /// <summary>Deletes the one-frame components the card demo's simulation owns.</summary>
    /// <remarks>
    /// The three deck commands are each their own entity, so the entity goes with the component.
    /// <see cref="MoveCompletedCommand"/> is the exception and the reason a Cleanup system cannot
    /// be one generic loop: it rides on the card entity the completion is about, so only the
    /// component is dropped — deleting the entity would delete the card.
    /// </remarks>
    internal sealed class DeckCleanupSystem : IEcsCleanup, IEcsInject<EcsWorld>
    {
        private EcsWorld _world;
        private EcsTagPool<MoveCompletedCommand> _completedMoves;

        public void Cleanup()
        {
            foreach (var entityId in _world.Where(out SingleAspect<DealDeckCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<ResetDeckCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<SetDeckSpeedCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleTagAspect<MoveCompletedCommand> _))
                _completedMoves.Del(entityId);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _completedMoves = obj.GetPool<MoveCompletedCommand>();
        }
    }
}
