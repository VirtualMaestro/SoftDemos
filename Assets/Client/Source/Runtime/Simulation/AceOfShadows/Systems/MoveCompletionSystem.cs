using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Messages;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Systems
{
    /// <summary>
    /// When a card's move finishes, lands it in the target stack (index, order, stack counters)
    /// and marks the whole deal complete after the last card.
    /// </summary>
    public sealed class MoveCompletionSystem : IEcsRun, IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private EcsWorld _world;
        private ILog _log;

        public void Run()
        {
            ref var state = ref _world.Get<DeckStateComp>();

            foreach (var entityId in _world.Where(out CompletionAspect aspect))
            {
                ref var card = ref aspect.Cards.Get(entityId);
                var targetStack = state.TargetStack;
                var targetOrder = -1;

                if (aspect.Moving.Has(entityId))
                {
                    ref readonly var moving = ref aspect.Moving.Read(entityId);
                    targetStack = moving.TargetStack;
                    targetOrder = moving.TargetOrder;
                }
                else
                    _log.Warn($"Entity {entityId}: move completed without MovingComp; treating it as landed.");

                foreach (var stackEntity in _world.Where(out StackAspect stacks))
                {
                    ref var stack = ref stacks.Stacks.Get(stackEntity);

                    if (stack.Index != targetStack)
                        continue;

                    card.StackIndex = targetStack;
                    card.OrderInStack = targetOrder >= 0 ? targetOrder : stack.Count;
                    stack.Count++;
                    break;
                }

                state.MovesCompleted++;
                aspect.Completed.TryDel(entityId);
                aspect.Moving.TryDel(entityId);

                if (!state.IsComplete && state.MovesCompleted == state.TotalCards)
                    state.IsComplete = true;
            }
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class CompletionAspect : EcsAspect
        {
            public readonly EcsTagPool<MoveCompletedTag> Completed = Inc;
            public readonly EcsPool<CardComp> Cards = Inc;
            public readonly EcsPool<MovingComp> Moving = Opt;
        }

        private sealed class StackAspect : EcsAspect
        {
            public readonly EcsPool<StackComp> Stacks = Inc;
        }
    }
}
