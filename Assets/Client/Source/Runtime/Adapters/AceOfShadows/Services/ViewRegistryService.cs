using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Views;
using UnityEngine;

namespace Client.Adapters.AceOfShadows.Services
{
    /// <summary>Resolves a <c>ViewHandleComp.Id</c> to a <see cref="Transform"/>.</summary>
    /// <remarks>This is the only place that knows both a handle number and a scene object.</remarks>
    public sealed class ViewRegistryService
    {
        private readonly Dictionary<int, Transform> _views = new();
        private readonly Dictionary<int, CardView> _cards = new();
        private int _nextId;

        public int Count => _views.Count;

        /// <summary>Registers a view and returns the handle the simulation will carry.</summary>
        public int Register(Transform view, CardView card = null)
        {
            var id = ++_nextId;
            _views.Add(id, view);

            if (card != null)
                _cards.Add(id, card);
            return id;
        }

        public bool TryResolve(int handleId, out Transform view)
        {
            return TryResolve(handleId, out view, out _);
        }

        public bool TryResolve(int handleId, out Transform view, out CardView card)
        {
            // A destroyed GameObject stays in the dictionary but compares equal to null.
            // Report it as unresolved instead of returning a dead Transform.
            if (_views.TryGetValue(handleId, out view) && view != null)
            {
                _cards.TryGetValue(handleId, out card);
                return true;
            }

            view = null;
            card = null;
            return false;
        }

        public bool Unregister(int handleId)
        {
            _cards.Remove(handleId);
            return _views.Remove(handleId);
        }

        /// <summary>Every live view. Use it to kill tweens that would outlive the world.</summary>
        public IEnumerable<Transform> Views => _views.Values;

        public void Clear()
        {
            _views.Clear();
            _cards.Clear();
        }
    }
}
