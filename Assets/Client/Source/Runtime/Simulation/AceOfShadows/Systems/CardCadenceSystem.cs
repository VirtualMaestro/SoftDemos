using Client.Simulation.Messages;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows
{
    /// <summary>
    /// Once per interval picks the top card of the source stack and issues a move command for it,
    /// keeping the leftover time so the rhythm stays even.
    /// </summary>
    public sealed class CardCadenceSystem : IEcsRun, IEcsInject<EcsWorld>,
        IEcsInject<ITimeService>, IEcsInject<ILog>
    {
        private EcsWorld _world;
        private ITimeService _time;
        private ILog _log;

        public void Run()
        {
            ref var state = ref _world.Get<DeckStateComp>();

            if (!state.IsDealt)
                return;

            if (state.MovesIssued >= state.TotalCards)
                return;

            state.SecondsUntilNextMove -= _time.DeltaSeconds;

            if (state.SecondsUntilNextMove > 0f)
                return;

            var overshoot = -state.SecondsUntilNextMove;
            state.SecondsUntilNextMove = overshoot >= state.MoveIntervalSeconds
                ? state.MoveIntervalSeconds
                : state.MoveIntervalSeconds - overshoot;

            var selectedEntity = -1;
            var highestOrder = int.MinValue;
            CardAspect cards = null;
            foreach (var entityId in _world.Where(out cards))
            {
                ref readonly var card = ref cards.Cards.Read(entityId);

                if (card.StackIndex != state.SourceStack || card.OrderInStack <= highestOrder)
                    continue;

                selectedEntity = entityId;
                highestOrder = card.OrderInStack;
            }

            if (selectedEntity < 0)
            {
                // At most one warn per move interval; the timer above gates this path.
                _log.Warn($"No movable card found with {state.MovesIssued}/{state.TotalCards} move(s) issued.");
                return;
            }

            var targetStackCount = 0;
            foreach (var stackEntity in _world.Where(out StackAspect stacks))
            {
                ref var stack = ref stacks.Stacks.Get(stackEntity);

                if (stack.Index == state.SourceStack)
                    stack.Count--;
                else if (stack.Index == state.TargetStack)
                    targetStackCount = stack.Count;
            }

            var targetOrder = targetStackCount + state.MovesIssued - state.MovesCompleted;
            ref var command = ref cards.Commands.Add(selectedEntity);
            command.TargetSlot = state.TargetStack;
            command.TargetDepth = targetOrder;
            command.Duration = state.MoveDurationSeconds;

            ref var moving = ref cards.Moving.Add(selectedEntity);
            moving.TargetStack = state.TargetStack;
            moving.TargetOrder = targetOrder;

            state.MovesIssued++;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ITimeService obj) => _time = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class CardAspect : EcsAspect
        {
            public readonly EcsPool<CardComp> Cards = Inc;
            public readonly EcsPool<MovingComp> Moving = Exc;
            public readonly EcsPool<MoveCommand> Commands = Opt;
        }

        private sealed class StackAspect : EcsAspect
        {
            public readonly EcsPool<StackComp> Stacks = Inc;
        }
    }
}
