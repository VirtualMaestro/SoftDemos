using Client.Simulation.Core.Phases;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Systems
{
    /// <summary>
    /// Consumes Deal/Reset commands: creates (or deletes) the card and stack entities and fills
    /// the deck state from config.
    /// </summary>
    internal sealed class DeckSetupSystem : IEcsSim, IEcsInject<EcsWorld>, IEcsInject<ILogService>
    {
        private readonly AceOfShadowsConfig _config;

        private EcsWorld _world;
        private ILogService _log;

        public DeckSetupSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Sim()
        {
            ref var state = ref _world.Get<DeckStateComp>();

            // The reset loop is written above the deal loop, and the cost of swapping them is
            // what is worth recording: closing and reopening a demo produces both commands at once,
            // and dealing into a deck that is then wiped opens the demo empty.
            foreach (var resetEntity in _world.Where(out ResetCommandAspect _))
            {
                if (state.IsDealt)
                    _Reset();
                else
                    _log.Warn("ResetDeckCommand ignored because the deck is not dealt.");
            }

            foreach (var dealEntity in _world.Where(out DealCommandAspect _))
            {
                if (state.IsDealt)
                    _Reset();

                _Deal(ref state);
            }
        }

        private void _Deal(ref DeckStateComp state)
        {
            state.TotalCards = _config.CardCount;
            state.SourceStack = _config.SourceStack;
            state.TargetStack = _config.TargetStack;
            state.MoveIntervalSeconds = _config.MoveIntervalSeconds;
            state.MoveDurationSeconds = _config.MoveDurationSeconds;
            state.SpeedMultiplier = 1f;
            state.SecondsUntilNextMove = _config.MoveIntervalSeconds;

            var stacks = _world.GetPool<StackComp>();

            for (var stackIndex = 0; stackIndex < _config.StackCount; stackIndex++)
            {
                var entityId = _world.NewEntity();
                ref var stack = ref stacks.Add(entityId);
                stack.Index = stackIndex;
                stack.Count = stackIndex == _config.SourceStack ? _config.CardCount : 0;
            }

            var cards = _world.GetPool<CardComp>();

            for (var order = 0; order < _config.CardCount; order++)
            {
                var entityId = _world.NewEntity();
                ref var card = ref cards.Add(entityId);
                card.StackIndex = _config.SourceStack;
                card.OrderInStack = order;
            }

            state.IsDealt = true;
        }

        /// <summary>
        /// Takes no state: the world component IS the state, and a parameter that only ever gets
        /// wiped hides the write from the call site while carrying nothing in.
        /// </summary>
        private void _Reset()
        {
            foreach (var entityId in _world.Where(out CardAspect _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out StackAspect _))
                _world.DelEntity(entityId);

            // Wipes the counters _Deal never touches - MovesIssued, MovesCompleted, IsComplete -
            // which is what stops a re-deal inheriting the previous run's progress.
            _world.Get<DeckStateComp>() = default;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class DealCommandAspect : EcsAspect
        {
            public readonly EcsPool<DealDeckCommand> _ = Inc;
        }

        private sealed class ResetCommandAspect : EcsAspect
        {
            public readonly EcsPool<ResetDeckCommand> _ = Inc;
        }

        private sealed class CardAspect : EcsAspect
        {
            public readonly EcsPool<CardComp> _ = Inc;
        }

        private sealed class StackAspect : EcsAspect
        {
            public readonly EcsPool<StackComp> _ = Inc;
        }
    }
}
