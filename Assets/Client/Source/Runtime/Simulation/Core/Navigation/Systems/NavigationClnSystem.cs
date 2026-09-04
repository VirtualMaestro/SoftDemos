using Client.Simulation.Core.Phases;
using Client.Simulation.Core.Navigation.Components.Commands;
using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Navigation.Systems
{
    /// <summary>Deletes the one-frame components the navigation feature owns.</summary>
    /// <remarks>
    /// Both commands are their own entity, so the entity goes with the component. Every delete is
    /// written out here rather than behind a generic helper: a helper body is where the analyzer
    /// stops looking, and the deletions are the one thing this system exists to make visible.
    /// </remarks>
    internal sealed class NavigationClnSystem : IEcsCleanup, IEcsInject<EcsWorld>
    {
        private EcsWorld _world;

        public void Cleanup()
        {
            foreach (var entityId in _world.Where(out SingleAspect<OpenDemoCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<CloseDemoCommand> _))
                _world.DelEntity(entityId);
        }

        public void Inject(EcsWorld obj) => _world = obj;
    }
}
