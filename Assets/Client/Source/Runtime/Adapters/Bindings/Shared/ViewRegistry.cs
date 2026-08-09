using System.Collections.Generic;
using Game.Adapters.Views;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Turns the simulation's opaque <c>ViewHandleComp.Id</c> back into a real
    /// <see cref="Transform"/>. The only place in the project that knows both a handle number and
    /// a scene object.
    /// </summary>
    public sealed class ViewRegistry
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
            // A destroyed GameObject leaves a non-null dictionary entry that compares equal to
            // null through Unity's overload. Treat it as unresolvable rather than handing a
            // caller a dead Transform.
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

        /// <summary>Every live view. Used to kill tweens that would outlive the world.</summary>
        public IEnumerable<Transform> Views => _views.Values;

        public void Clear()
        {
            _views.Clear();
            _cards.Clear();
        }
    }
}
