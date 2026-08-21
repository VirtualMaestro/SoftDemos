using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Views;
using DCFApixels.DragonECS;
using DG.Tweening;
using UnityEngine;

namespace Client.Adapters.AceOfShadows.Services
{
    /// <summary>Plays card-move tweens through DOTween and queues their completions.</summary>
    /// <remarks>
    /// This is not a system, and it never touches the world: the <see cref="entlong"/> it takes
    /// is an opaque correlation token it hands back through <see cref="Completions"/>, and every
    /// world change stays in <c>TweenPlaybackSystem</c>, which drains the queue in one place at
    /// one point in the frame. The teardown calls are synchronous, because the caller destroys
    /// the views in the same frame.
    /// </remarks>
    public sealed class CardMovePlayerService
    {
        private readonly ViewRegistryService _views;
        private readonly List<entlong> _completedTweens = new();

        public CardMovePlayerService(ViewRegistryService views)
        {
            _views = views;
        }

        /// <summary>Tweens that finished but whose completion has not been applied yet.</summary>
        public bool HasCompletedTweens => _completedTweens.Count > 0;
        public IReadOnlyList<entlong> Completions => _completedTweens;

        public void ClearCompletions() => _completedTweens.Clear();

        public void StartMove(Transform view, CardView card, Vector3 target,
            float duration, entlong entity)
        {
            var tween = view.DOMove(target, duration).SetEase(Ease.OutCubic);

            if (card != null)
            {
                // Both closures are made once per move. OnUpdate does not allocate per frame.
                // The move eases out. The flip stays linear, so its midpoint is clear at high speed.
                tween.OnUpdate(() => card.OnMoveProgress(tween.ElapsedPercentage()));
            }

            tween.OnComplete(() => _completedTweens.Add(entity));
        }

        /// <summary>Kills the view tweens and nothing else.</summary>
        /// <remarks>
        /// No world cleanup here — a killed tween never calls back, and a completion already
        /// queued is dropped by the entity's generation check when the system drains the queue.
        /// The orphaned move components are the system's own business
        /// (<c>TweenPlaybackSystem._CancelOrphanedMoves</c>).
        /// </remarks>
        public void KillTweensFor(IReadOnlyList<int> handleIds)
        {
            foreach (var handleId in handleIds)
                if (_views.TryResolve(handleId, out var view))
                    DOTween.Kill(view);
        }

        /// <summary>Kills every move tween before the world goes away.</summary>
        /// <remarks>A tween that outlives its world calls back into a destroyed pipeline.</remarks>
        public void KillAll()
        {
            foreach (var view in _views.Views)
            {
                if (view == null)
                    continue;

                DOTween.Kill(view);
            }

            _completedTweens.Clear();
        }
    }
}
