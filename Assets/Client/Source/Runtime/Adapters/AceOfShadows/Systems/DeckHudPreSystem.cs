using Client.Simulation.Core.Phases;
using Client.Adapters.AceOfShadows.Components.Events;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.Shared.Services;
using Client.Simulation.AceOfShadows.Components;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>
    /// Mirrors deck state onto the HUD: stack counters, speed label, completion message, and
    /// counter positions after a layout change.
    /// </summary>
    internal sealed class DeckHudPreSystem : IEcsPresent, IEcsInject<EcsWorld>,
        IEcsInject<StackSlotLayoutService>, IEcsInject<ScreenRegistryService>
    {
        private static readonly Vector3 CounterOffset = new(0f, 1.5f, 0f);

        private EcsWorld _world;
        private StackSlotLayoutService _layout;
        private ScreenRegistryService _screens;
        private EcsTagPool<LayoutChangedEvent> _layoutChanged;
        // The screen's instance id, not the screen: a system holds no engine object (DEU0146).
        // It answers the one question the cache was for — is this the screen the cached counters
        // belong to — and the screen itself is resolved through its registry, per call.
        private int _screenInstanceId;
        private int _sourceCount = int.MinValue;
        private int _targetCount = int.MinValue;
        private int _totalCards = int.MinValue;
        private float _speedMultiplier = float.NaN;
        private bool _isComplete;

        public void Present()
        {
            if (!_screens.TryGet(out AceOfShadowsScreen scene))
            {
                _screenInstanceId = 0;
                return;
            }

            if (_screenInstanceId != scene.GetInstanceID())
                _ResetFor(scene);

            ref readonly var state = ref _world.Get<DeckStateComp>();

            if (!state.IsDealt)
                return;

            var sourceCount = 0;
            var targetCount = 0;

            foreach (var entityId in _world.Where(out StackAspect aspect))
            {
                ref readonly var stack = ref aspect.Stacks.Read(entityId);

                if (stack.Index == state.SourceStack)
                    sourceCount = stack.Count;
                else if (stack.Index == state.TargetStack)
                    targetCount = stack.Count;
            }

            if (_sourceCount != sourceCount)
            {
                _sourceCount = sourceCount;
                scene.SourceCounter.SetText("{0}", sourceCount);
            }

            if (_targetCount != targetCount)
            {
                _targetCount = targetCount;
                scene.TargetCounter.SetText("{0}", targetCount);
            }

            if (!Mathf.Approximately(_speedMultiplier, state.SpeedMultiplier))
            {
                _speedMultiplier = state.SpeedMultiplier;
                scene.SpeedLabel.SetText("×{0:0}", _speedMultiplier);
            }

            if (_isComplete != state.IsComplete)
            {
                _isComplete = state.IsComplete;
                scene.CompletionLabel.gameObject.SetActive(_isComplete);

                if (_isComplete)
                    scene.CompletionLabel.SetText("All {0} cards moved.", state.TotalCards);
            }

            if (_layoutChanged.Count == 0 && _totalCards == state.TotalCards)
                return;

            _totalCards = state.TotalCards;
            scene.SourceCounter.transform.position =
                _layout.SlotPosition(state.SourceStack, state.TotalCards) + CounterOffset;
            scene.TargetCounter.transform.position =
                _layout.SlotPosition(state.TargetStack, state.TotalCards) + CounterOffset;
        }

        private void _ResetFor(AceOfShadowsScreen scene)
        {
            _screenInstanceId = scene.GetInstanceID();
            _sourceCount = int.MinValue;
            _targetCount = int.MinValue;
            _totalCards = int.MinValue;
            _speedMultiplier = float.NaN;
            _isComplete = false;
            scene.CompletionLabel.gameObject.SetActive(false);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _layoutChanged = obj.GetPool<LayoutChangedEvent>();
        }

        public void Inject(StackSlotLayoutService obj) => _layout = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

        private sealed class StackAspect : EcsAspect
        {
            public readonly EcsPool<StackComp> Stacks = Inc;
        }
    }
}
