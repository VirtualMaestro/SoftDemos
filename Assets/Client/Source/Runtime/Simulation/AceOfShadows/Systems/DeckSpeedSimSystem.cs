using Client.Simulation.Core.Phases;
using Client.Simulation.AceOfShadows.Components;
using Client.Simulation.AceOfShadows.Components.Commands;
using Client.Simulation.Core.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.AceOfShadows.Systems
{
    /// <summary>
    /// Consumes speed-change commands: clamps the multiplier to 1–8 and recomputes the move
    /// interval and duration.
    /// </summary>
    internal sealed class DeckSpeedSimSystem : IEcsSim, IEcsInject<EcsWorld>, IEcsInject<ILogService>
    {
        private readonly AceOfShadowsConfig _config;

        private EcsWorld _world;
        private ILogService _log;

        public DeckSpeedSimSystem(AceOfShadowsConfig config)
        {
            _config = config;
        }

        public void Sim()
        {
            ref var state = ref _world.Get<DeckStateComp>();

            foreach (var entityId in _world.Where(out CommandAspect aspect))
            {
                var requestedMultiplier = aspect.Commands.Read(entityId).Multiplier;

                if (!state.IsDealt)
                    _log.Warn("SetDeckSpeedCommand ignored because the deck is not dealt.");
                else if (float.IsNaN(requestedMultiplier) || float.IsInfinity(requestedMultiplier) ||
                    requestedMultiplier <= 0f)
                    _log.Warn($"SetDeckSpeedCommand ignored invalid multiplier {requestedMultiplier}.");
                else
                    _Apply(ref state, requestedMultiplier);
            }
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
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;

        private sealed class CommandAspect : EcsAspect
        {
            public readonly EcsPool<SetDeckSpeedCommand> Commands = Inc;
        }
    }
}
