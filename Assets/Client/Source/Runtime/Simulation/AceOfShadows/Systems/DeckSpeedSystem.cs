using System.Collections.Generic;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows
{
    public sealed class DeckSpeedSystem : IEcsRun, IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private readonly AceOfShadowsConfig _config;
        private readonly List<int> _entitiesToDelete = new();

        private EcsWorld _world;
        private ILog _log;

        public DeckSpeedSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Run()
        {
            ref var state = ref _world.Get<DeckStateComp>();
            foreach (var entityId in _world.Where(out CommandAspect aspect))
            {
                var requestedMultiplier = aspect.Commands.Read(entityId).Multiplier;

                if (state.IsDealt == false)
                    _log.Warn("SetDeckSpeedCommand ignored because the deck is not dealt.");
                else if (float.IsNaN(requestedMultiplier) || float.IsInfinity(requestedMultiplier) ||
                    requestedMultiplier <= 0f)
                    _log.Warn($"SetDeckSpeedCommand ignored invalid multiplier {requestedMultiplier}.");
                else
                    _Apply(ref state, requestedMultiplier);

                _entitiesToDelete.Add(entityId);
            }

            foreach (var entityId in _entitiesToDelete)
                _world.DelEntity(entityId);

            _entitiesToDelete.Clear();
        }

        private void _Apply(ref DeckStateComp state, float requestedMultiplier)
        {
            var multiplier = requestedMultiplier < 1f
                ? 1f
                : requestedMultiplier > 8f
                    ? 8f
                    : requestedMultiplier;
            var interval = _config.MoveIntervalSeconds / multiplier;
            var duration = _config.MoveDurationSeconds;
            var maximumDuration = interval * 0.8f;

            if (duration > maximumDuration)
                duration = maximumDuration;

            state.MoveIntervalSeconds = interval;
            state.MoveDurationSeconds = duration;

            if (state.SecondsUntilNextMove > interval)
                state.SecondsUntilNextMove = interval;
            state.SpeedMultiplier = multiplier;

            _log.Info($"Deck speed ×{multiplier:0.###}: interval {interval:0.###}s, duration {duration:0.###}s.");
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class CommandAspect : EcsAspect
        {
            public EcsPool<SetDeckSpeedCommand> Commands = Inc;
        }
    }
}
