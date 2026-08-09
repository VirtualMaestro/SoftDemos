using DCFApixels.DragonECS;
using Game.Adapters.Views;
using Game.Simulation.AceOfShadows;
using Game.Simulation.Messages;
using Game.Simulation.Ports;

namespace Game.Adapters.Bindings
{
    public sealed class CardBindingSystem : IEcsLateRun, IEcsInject<EcsWorld>, IEcsInject<ILog>
    {
        private const int InFlightSortingBase = 500;

        private readonly ViewRegistry _views;
        private readonly StackSlotLayout _layout;
        private readonly CardViewChannel _channel;

        private EcsWorld _world;
        private ILog _log;
        private int _bindCursor;
        private int _bindingResetVersion;
        private int _seatingVersion;
        private bool _warnedOutOfViews;
        private bool _loggedAllBound;

        public CardBindingSystem(ViewRegistry views, StackSlotLayout layout, CardViewChannel channel)
        {
            _views = views;
            _layout = layout;
            _channel = channel;
        }

        public void LateRun()
        {
            if (AceOfShadowsScreen.Current == null)
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
