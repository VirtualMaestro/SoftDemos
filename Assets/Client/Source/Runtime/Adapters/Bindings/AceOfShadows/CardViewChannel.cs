using System;
using System.Collections.Generic;
using Game.Adapters.Views;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// The Ace of Shadows card-view channel, shared between <see cref="AceOfShadowsStageSystem"/>
    /// (which fills and clears it) and <see cref="CardBindingSystem"/> (which reads it). A plain
    /// collaborator rather than properties on the stage system, because systems must never hold
    /// other systems (see SystemIsolationTests). The stage system stays owner of every view and
    /// sprite lifetime; this object only carries the references and the two change counters —
    /// it is not an object pool, nothing is acquired or released through it.
    /// </summary>
    public sealed class CardViewChannel
    {
        private readonly List<CardView> _views = new();
        private readonly List<int> _handles = new();

        private Sprite _cardBack;
        private Sprite[] _faces = Array.Empty<Sprite>();

        public IReadOnlyList<CardView> Views => _views;
        public IReadOnlyList<int> Handles => _handles;
        public int BindingResetVersion { get; private set; }
        public int SeatingVersion { get; private set; }

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

        public void BumpBindingReset() => BindingResetVersion++;
        public void BumpSeating() => SeatingVersion++;

        public void Clear()
        {
            _views.Clear();
            _handles.Clear();
            _cardBack = null;
            _faces = Array.Empty<Sprite>();
        }
    }
}
