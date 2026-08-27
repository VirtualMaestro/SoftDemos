using System;
using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Views;
using UnityEngine;

namespace Client.Adapters.AceOfShadows
{
    /// <summary>Shared card views for Ace of Shadows. The stage system writes, the binding system reads.</summary>
    /// <remarks>
    /// The stage system owns every view and sprite. This object holds the handle-to-view map and
    /// nothing else — every change the binding system has to notice travels through the world as
    /// an event. It is not a pool: nothing is acquired or released here.
    /// </remarks>
    public sealed class CardViewChannel
    {
        private readonly List<CardView> _views = new();
        private readonly List<int> _handles = new();

        private Sprite _cardBack;
        private Sprite[] _faces = Array.Empty<Sprite>();

        public IReadOnlyList<CardView> Views => _views;
        public IReadOnlyList<int> Handles => _handles;

        public void SetSprites(Sprite cardBack, Sprite[] faces)
        {
            _cardBack = cardBack;
            _faces = faces ?? Array.Empty<Sprite>();
        }

        public void Add(CardView view, int handleId)
        {
            _views.Add(view);
            _handles.Add(handleId);
        }

        public void ConfigureCard(int cardIndex, CardView cardView) =>
            cardView.Configure(_cardBack, _faces[cardIndex % _faces.Length]);

        public void Clear()
        {
            _views.Clear();
            _handles.Clear();
            _cardBack = null;
            _faces = Array.Empty<Sprite>();
        }
    }
}
