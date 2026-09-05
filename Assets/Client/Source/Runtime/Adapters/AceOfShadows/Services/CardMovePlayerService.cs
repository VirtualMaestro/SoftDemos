using System;
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
    /// world change stays in <c>TweenPlaybackPreSystem</c>, which drains the queue in one place at
    /// one point in the frame. The teardown calls are synchronous, because the caller destroys
    /// the views in the same frame.
    /// An owner disposes what it hands out: the composition root disposes this service, and no
    /// system kills a tween on destroy.
    /// </remarks>
    public sealed class CardMovePlayerService : IDisposable
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

        /// <summary>Queues a completion for a flight that will never run.</summary>
        /// <remarks>
        /// The caller found no view to move. The simulation is waiting for this card either way, so
        /// the failure enters by the same door a real completion does — which keeps the queue the
        /// one and only source of completions, and keeps the world write in the one phase that is
        /// allowed to make it.
        /// </remarks>
        public void ReportCompleted(entlong entity) => _completedTweens.Add(entity);

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

        /// <summary>Kills the view tweens of one contiguous handle range and nothing else.</summary>
        /// <remarks>
        /// No world cleanup here — a killed tween never calls back, and a completion already
        /// queued is dropped by the entity's generation check when the system drains the queue.
        /// The orphaned move components are the system's own business
        /// (<c>TweenPlaybackPreSystem._CancelOrphanedMoves</c>).
        /// </remarks>
        public void KillTweensFor(int firstHandle, int count)
        {
            for (var handleId = firstHandle; handleId < firstHandle + count; handleId++)
                if (_views.TryResolve(handleId, out var view))
                    DOTween.Kill(view);
        }

        /// <summary>Kills every move tween before the world goes away.</summary>
        /// <remarks>
        /// A tween that outlives its world calls back into a destroyed pipeline. The composition
        /// root calls this BEFORE it disposes the view registry, because the tweens are found
        /// through the views the registry still holds.
        /// </remarks>
        public void Dispose()
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
