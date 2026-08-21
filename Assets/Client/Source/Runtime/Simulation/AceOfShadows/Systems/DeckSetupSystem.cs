using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.Shared.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Systems
{
    /// <summary>
    /// Consumes Deal/Reset commands: creates (or deletes) the card and stack entities and fills
    /// the deck state from config.
    /// </summary>
    public sealed class DeckSetupSystem : IEcsRun, IEcsInject<EcsWorld>, IEcsInject<ILogService>
    {
        private readonly AceOfShadowsConfig _config;

        private EcsWorld _world;
        private ILogService _log;

        public DeckSetupSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Run()
        {
            ref var state = ref _world.Get<DeckStateComp>();

            // Reset is consumed before Deal so that a close-then-open in the same tick leaves the
            // deck dealt. The reverse order would deal and then wipe it, and the demo would open
            // empty.
            foreach (var entityId in _world.Where(out ResetCommandAspect _))
            {
                if (state.IsDealt)
                    _Reset(ref state);
                else
                    _log.Warn("ResetDeckCommand ignored because the deck is not dealt.");

                _world.DelEntity(entityId);
            }

            foreach (var entityId in _world.Where(out DealCommandAspect _))
            {
                if (state.IsDealt)
                    _Reset(ref state);

                _Deal(ref state);
                _world.DelEntity(entityId);
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

        private void _Reset(ref DeckStateComp state)
        {
            foreach (var entityId in _world.Where(out CardAspect _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out StackAspect _))
                _world.DelEntity(entityId);

            state = default;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class DealCommandAspect : EcsAspect
        {
            public EcsPool<DealDeckCommand> Commands = Inc;
        }

        private sealed class ResetCommandAspect : EcsAspect
        {
            public EcsPool<ResetDeckCommand> Commands = Inc;
        }

        private sealed class CardAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
        }

        private sealed class StackAspect : EcsAspect
        {
            public EcsPool<StackComp> Stacks = Inc;
        }
    }
}
