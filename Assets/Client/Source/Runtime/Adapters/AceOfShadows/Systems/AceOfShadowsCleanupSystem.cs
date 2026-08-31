using Client.Simulation.Core.Phases;
using Client.Adapters.AceOfShadows.Components;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>Deletes the one-frame components Ace of Shadows' adapter owns.</summary>
    /// <remarks>
    /// An <c>*Event</c> lives from its <c>Add</c> until this system deletes it, and nothing else
    /// may delete one — which is what makes the suffix readable as a lifetime. Each event is its
    /// own entity, so the entity goes with it. This becomes an <c>IEcsCleanup</c> system unchanged
    /// once the phase interfaces exist.
    /// </remarks>
    internal sealed class AceOfShadowsCleanupSystem : IEcsCleanup, IEcsInject<EcsWorld>
    {
        private EcsWorld _world;

        public void Cleanup()
        {
            foreach (var entityId in _world.Where(out ViewsResetAspect _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out LayoutChangedAspect _))
                _world.DelEntity(entityId);
        }

        public void Inject(EcsWorld obj) => _world = obj;

        private sealed class ViewsResetAspect : EcsAspect
        {
            public readonly EcsTagPool<ViewsResetEvent> _ = Inc;
        }

        private sealed class LayoutChangedAspect : EcsAspect
        {
            public readonly EcsTagPool<LayoutChangedEvent> _ = Inc;
        }
    }
}
