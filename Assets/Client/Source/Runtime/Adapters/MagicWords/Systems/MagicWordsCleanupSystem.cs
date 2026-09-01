using Client.Simulation.Core.Phases;
using Client.Adapters.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords.Systems
{
    /// <summary>Deletes the one-frame components Magic Words' adapter owns.</summary>
    /// <remarks>
    /// An <c>*Event</c> lives from its <c>Add</c> until this system deletes it, and nothing else
    /// may delete one — which is what makes the suffix readable as a lifetime. Each event is its
    /// own entity, so the entity goes with it. This becomes an <c>IEcsCleanup</c> system unchanged
    /// once the phase interfaces exist.
    /// </remarks>
    internal sealed class MagicWordsCleanupSystem : IEcsCleanup, IEcsInject<EcsWorld>
    {
        private EcsWorld _world;

        public void Cleanup()
        {
            foreach (var entityId in _world.Where(out LogResetAspect _))
                _world.DelEntity(entityId);
        }

        public void Inject(EcsWorld obj) => _world = obj;

        private sealed class LogResetAspect : EcsAspect
        {
            public readonly EcsTagPool<DialogueLogResetEvent> _ = Inc;
        }
    }
}
