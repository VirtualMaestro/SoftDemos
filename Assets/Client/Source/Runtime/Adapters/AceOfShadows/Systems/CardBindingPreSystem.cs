using Client.Simulation.Core.Phases;
using Client.Adapters.AceOfShadows.Components;
using Client.Adapters.AceOfShadows.Components.Events;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Views;
using Client.Adapters.Shared.Services;
using Client.Simulation.AceOfShadows.Components;
using DCFApixels.DragonECS;

namespace Client.Adapters.AceOfShadows.Systems
{
    /// <summary>
    /// Keeps resting cards seated at their slot position and raises the sorting order of cards in
    /// flight.
    /// </summary>
    /// <remarks>
    /// It used to bind the views too. Binding moved to <c>AceOfShadowsInpSystem</c> with the cursor
    /// it depends on: the face a card shows is chosen by the bind ORDER and never by the entity, so
    /// the half that owns the cursor is the half that can choose. What is left is what this system
    /// can answer from the world alone — where a card sits and how high it draws
    /// (adr-an-engine-object-has-one-owner-per-kind).
    /// </remarks>
    internal sealed class CardBindingPreSystem : IEcsPresent, IEcsInject<EcsWorld>,
        IEcsInject<ViewRegistryService>, IEcsInject<StackSlotLayoutService>,
        IEcsInject<ScreenRegistryService>
    {
        private const int InFlightSortingBase = 500;

        private EcsWorld _world;
        private ViewRegistryService _views;
        private StackSlotLayoutService _layout;
        private ScreenRegistryService _screens;
        private EcsTagPool<LayoutChangedEvent> _layoutChanged;

        public void Present()
        {
            if (!_screens.TryGet<AceOfShadowsScreen>(out _))
                return;

            if (_layoutChanged.Count > 0)
                _InvalidateSeating();

            _RaiseMovingCards();
            _SeatRestingCards();
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

                if (!_views.TryResolve(handleId, out var transform, out var cardView) ||
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

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _layoutChanged = obj.GetPool<LayoutChangedEvent>();
        }

        public void Inject(ViewRegistryService obj) => _views = obj;
        public void Inject(StackSlotLayoutService obj) => _layout = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

        private sealed class MovingAspect : EcsAspect
        {
            public readonly EcsPool<CardComp> Cards = Inc;
            public readonly EcsPool<MovingComp> _ = Inc;
            public readonly EcsPool<ViewHandleComp> Views = Inc;
            public readonly EcsTagPool<CardSeatedTag> Seated = Inc;
        }

        private sealed class RestingAspect : EcsAspect
        {
            public readonly EcsPool<CardComp> Cards = Inc;
            public readonly EcsPool<MovingComp> _ = Exc;
            public readonly EcsPool<ViewHandleComp> Views = Inc;
            public readonly EcsTagPool<CardSeatedTag> Seated = Exc;
        }

        private sealed class SeatedAspect : EcsAspect
        {
            public readonly EcsTagPool<CardSeatedTag> Seated = Inc;
        }
    }
}
