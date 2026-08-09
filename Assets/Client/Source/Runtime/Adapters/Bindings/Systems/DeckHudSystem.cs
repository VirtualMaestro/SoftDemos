using DCFApixels.DragonECS;
using Game.Adapters.Views;
using Game.Simulation.AceOfShadows;
using Game.Simulation.Ports;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    public sealed class DeckHudSystem : IEcsLateRun, IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private static readonly Vector3 _counterOffset = new(0f, 1.5f, 0f);

        private readonly StackSlotLayout _layout;

        private EcsWorld _world;
        private ILog _log;
        private AceOfShadowsScreen _scene;
        private int _sourceCount = int.MinValue;
        private int _targetCount = int.MinValue;
        private int _totalCards = int.MinValue;
        private int _layoutVersion = -1;
        private float _speedMultiplier = float.NaN;
        private bool _isComplete;

        public DeckHudSystem(StackSlotLayout layout)
        {
            _layout = layout;
        }

        public void LateRun()
        {
            var current = AceOfShadowsScreen.Current;

            if (current == null)
            {
                _scene = null;
                return;
            }

            if (_scene != current)
                _ResetFor(current);

            ref readonly var state = ref _world.Get<DeckStateComp>();

            if (state.IsDealt == false)
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
                _scene.SourceCounter.SetText("{0}", sourceCount);
            }

            if (_targetCount != targetCount)
            {
                _targetCount = targetCount;
                _scene.TargetCounter.SetText("{0}", targetCount);
            }

            var speedMultiplier = state.IsDealt ? state.SpeedMultiplier : 1f;

            if (_speedMultiplier != speedMultiplier)
            {
                _speedMultiplier = speedMultiplier;
                _scene.SpeedLabel.SetText("×{0:0}", speedMultiplier);
            }

            if (_isComplete != state.IsComplete)
            {
                _isComplete = state.IsComplete;
                _scene.CompletionLabel.gameObject.SetActive(_isComplete);

                if (_isComplete)
                {
                    _scene.CompletionLabel.SetText("All {0} cards moved.", state.TotalCards);
                    _log.Info("Completion message shown.");
                }
            }

            if (_layoutVersion == _layout.Version && _totalCards == state.TotalCards)
                return;

            _layoutVersion = _layout.Version;
            _totalCards = state.TotalCards;
            _scene.SourceCounter.transform.position =
                _layout.SlotPosition(state.SourceStack, state.TotalCards) + _counterOffset;
            _scene.TargetCounter.transform.position =
                _layout.SlotPosition(state.TargetStack, state.TotalCards) + _counterOffset;
        }

        private void _ResetFor(AceOfShadowsScreen scene)
        {
            _scene = scene;
            _sourceCount = int.MinValue;
            _targetCount = int.MinValue;
            _totalCards = int.MinValue;
            _layoutVersion = -1;
            _speedMultiplier = float.NaN;
            _isComplete = false;
            _scene.CompletionLabel.gameObject.SetActive(false);
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class StackAspect : EcsAspect
        {
            public EcsPool<StackComp> Stacks = Inc;
        }
    }
}
