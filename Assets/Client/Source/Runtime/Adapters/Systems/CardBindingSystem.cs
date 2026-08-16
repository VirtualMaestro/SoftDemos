using Client.Adapters.Components;
using Client.Adapters.Services;
using Client.Adapters.Shared;
using Client.Adapters.Views;
using Client.Simulation.AceOfShadows;
using Client.Simulation.Messages;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Adapters.Systems
{
    public sealed class CardBindingSystem : IEcsLateRun, IEcsInject<EcsWorld>, IEcsInject<ILog>,
        IEcsInject<ViewRegistryService>, IEcsInject<StackSlotLayoutService>, IEcsInject<CardViewChannel>,
        IEcsInject<ScreenRegistryService>
    {
        private const int InFlightSortingBase = 500;

        private EcsWorld _world;
        private ILog _log;
        private ViewRegistryService _views;
        private StackSlotLayoutService _layout;
        private CardViewChannel _channel;
        private ScreenRegistryService _screens;
        private int _bindCursor;
        private int _bindingResetVersion;
        private int _seatingVersion;
        private bool _warnedOutOfViews;
        private bool _loggedAllBound;

        public void LateRun()
        {
            if (_screens.TryGet<AceOfShadowsScreen>(out _) == false)
                return;

            if (_bindingResetVersion != _channel.BindingResetVersion)
            {
                _bindingResetVersion = _channel.BindingResetVersion;
                ResetBindings();
            }
            else if (_seatingVersion != _channel.SeatingVersion)
                _InvalidateSeating();

            _seatingVersion = _channel.SeatingVersion;
            _BindUnboundCards();
            _RaiseMovingCards();
            _SeatRestingCards();
        }

        public void ResetBindings()
        {
            _bindCursor = 0;
            _warnedOutOfViews = false;
            _loggedAllBound = false;
            _InvalidateSeating();
        }

        private void _BindUnboundCards()
        {
            foreach (var entityId in _world.Where(out UnboundAspect aspect))
            {
                if (_bindCursor >= _channel.Views.Count || _bindCursor >= _channel.Handles.Count)
                {
                    if (_warnedOutOfViews == false)
                    {
                        _warnedOutOfViews = true;
                        _log.Warn($"Card view channel ran out of views after {_bindCursor} binding(s).");
                    }

                    return;
                }

                ref readonly var card = ref aspect.Cards.Read(entityId);
                var cardView = _channel.Views[_bindCursor];
                var handleId = _channel.Handles[_bindCursor];
                _channel.ConfigureCard(_bindCursor, cardView);
                cardView.ResetToBack();
                cardView.transform.position = _layout.SlotPosition(card.StackIndex, card.OrderInStack);
                cardView.SetSortingOrder(card.OrderInStack);
                aspect.Views.Add(entityId).Id = handleId;
                aspect.Seated.TryAdd(entityId);
                _bindCursor++;
            }

            if (_loggedAllBound == false && _bindCursor == _channel.Views.Count && _bindCursor > 0)
            {
                _loggedAllBound = true;
                _log.Info($"Bound {_bindCursor}/{_channel.Views.Count} card view(s).");
            }
        }

        private void _RaiseMovingCards()
        {
            foreach (var entityId in _world.Where(out MovingAspect aspect))
            {
                var handleId = aspect.Views.Read(entityId).Id;

                if (_views.TryResolve(handleId, out _, out var cardView) && cardView != null)
                    cardView.SetSortingOrder(InFlightSortingBase + aspect.Cards.Read(entityId).OrderInStack);

                aspect.Seated.TryDel(entityId);
            }
        }

        private void _SeatRestingCards()
        {
            foreach (var entityId in _world.Where(out RestingAspect aspect))
            {
                var handleId = aspect.Views.Read(entityId).Id;

                if (_views.TryResolve(handleId, out var transform, out var cardView) == false ||
                    cardView == null)
                    continue;

                ref readonly var card = ref aspect.Cards.Read(entityId);
                transform.position = _layout.SlotPosition(card.StackIndex, card.OrderInStack);
                cardView.SetSortingOrder(card.OrderInStack);
                cardView.MoveEnded();
                aspect.Seated.TryAdd(entityId);
            }
        }

        private void _InvalidateSeating()
        {
            foreach (var entityId in _world.Where(out SeatedAspect aspect))
                aspect.Seated.TryDel(entityId);
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
        public void Inject(ViewRegistryService obj) => _views = obj;
        public void Inject(StackSlotLayoutService obj) => _layout = obj;
        public void Inject(CardViewChannel obj) => _channel = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

        private sealed class UnboundAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
            public EcsPool<ViewHandleComp> Views = Exc;
            public EcsTagPool<CardSeatedTag> Seated = Opt;
        }

        private sealed class MovingAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
            public EcsPool<MovingComp> Moving = Inc;
            public EcsPool<ViewHandleComp> Views = Inc;
            public EcsTagPool<CardSeatedTag> Seated = Inc;
        }

        private sealed class RestingAspect : EcsAspect
        {
            public EcsPool<CardComp> Cards = Inc;
            public EcsPool<MovingComp> Moving = Exc;
            public EcsPool<ViewHandleComp> Views = Inc;
            public EcsTagPool<CardSeatedTag> Seated = Exc;
        }

        private sealed class SeatedAspect : EcsAspect
        {
            public EcsTagPool<CardSeatedTag> Seated = Inc;
        }
    }
}
