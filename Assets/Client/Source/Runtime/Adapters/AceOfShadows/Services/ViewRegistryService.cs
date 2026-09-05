using System;
using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Views;
using UnityEngine;

namespace Client.Adapters.AceOfShadows.Services
{
    /// <summary>Resolves a <c>ViewHandleComp.Id</c> to a <see cref="Transform"/>.</summary>
    /// <remarks>
    /// This is the only place that knows both a handle number and a scene object, so it is also
    /// the only place that may destroy one. An owner disposes what it hands out: the composition
    /// root disposes this service, and no system destroys a view on destroy.
    /// </remarks>
    public sealed class ViewRegistryService : IDisposable
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

        /// <summary>Destroys every view this registry handed out and forgets all of them.</summary>
        /// <remarks>
        /// The null guard is not defensive noise: at application quit the scene may already have
        /// destroyed the object, and Unity's fake-null comparison is the only sign of it.
        /// </remarks>
        public void Dispose()
        {
            foreach (var view in _views.Values)
                if (view != null)
                    UnityEngine.Object.Destroy(view.gameObject);

            _views.Clear();
            _cards.Clear();
        }
    }
}
