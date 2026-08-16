using System.Collections.Generic;
using Client.Adapters.Components;
using Client.Adapters.Views;
using Client.Simulation.Messages;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;
using DG.Tweening;
using UnityEngine;

namespace Client.Adapters.Services
{
    /// <summary>The only place that calls DOTween.</summary>
    /// <remarks>
    /// This is not a system. Several systems share the behaviour, so the composition root owns it,
    /// like <see cref="ViewRegistryService"/>. <see cref="TweenPlaybackSystem"/> drives it. The teardown
    /// calls are synchronous, because the caller destroys the views in the same frame.
    /// </remarks>
    public sealed class TweenPlayerService
    {
        private readonly EcsWorld _world;
        private readonly ViewRegistryService _views;
        private readonly ILog _log;
        private readonly List<entlong> _completions = new();
        private readonly List<CanvasGroup> _fading = new();

        public TweenPlayerService(EcsWorld world, ViewRegistryService views, ILog log)
        {
            _world = world;
            _views = views;
            _log = log;
        }

        /// <summary>Tweens that finished but whose completion has not been applied yet.</summary>
        public int PendingCompletionCount => _completions.Count;
        public int ActiveFadeCount => _fading.Count;
        public IReadOnlyList<entlong> Completions => _completions;

        public void ClearCompletions() => _completions.Clear();

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

            tween.OnComplete(() => _completions.Add(entity));
        }

        public void FadeIn(CanvasGroup group, float duration)
        {
            group.alpha = 0f;
            _fading.Add(group);
            DOTween.To(() => group.alpha, value => group.alpha = value, 1f, duration)
                .SetTarget(group)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _fading.Remove(group));
        }

        public int KillFades()
        {
            var killed = 0;
            foreach (var group in _fading)
                if (group != null)
                    killed += DOTween.Kill(group);

            _fading.Clear();
            return killed;
        }

        public int KillTweensFor(IReadOnlyList<int> handleIds)
        {
            var killed = 0;
            foreach (var handleId in handleIds)
                if (_views.TryResolve(handleId, out var view))
                    killed += DOTween.Kill(view);

            var commands = _world.GetPool<MoveCommand>();
            var running = _world.GetPool<TweenRunningTag>();

            foreach (var entityId in _world.Where(out KillAspect aspect))
            {
                var handleId = aspect.Views.Read(entityId).Id;

                if (_ContainsHandle(handleIds, handleId) == false)
                    continue;

                _completions.Remove(_world.GetEntityLong(entityId));
                running.TryDel(entityId);
                commands.TryDel(entityId);
            }

            _log.Info($"Killed {killed} tween(s) for {handleIds.Count} view handle(s).");
            return killed;
        }

        /// <summary>Kills every tween before the world goes away.</summary>
        /// <remarks>A tween that outlives its world calls back into a destroyed pipeline.</remarks>
        public void KillAll()
        {
            var faded = KillFades();
            var killed = 0;
            foreach (var view in _views.Views)
            {
                if (view == null)
                    continue;

                killed += DOTween.Kill(view);
            }

            _completions.Clear();
            _log.Info(
                $"Killed {killed} move tween(s) and {faded} fade tween(s) " +
                $"across {_views.Count} registered view(s).");
        }

        private static bool _ContainsHandle(IReadOnlyList<int> handleIds, int handleId)
        {
            // This is O(n²), but it runs only on teardown. Add a handle map if the pools grow.
            foreach (var id in handleIds)
                if (id == handleId)
                    return true;

            return false;
        }

        private sealed class KillAspect : EcsAspect
        {
            public EcsPool<ViewHandleComp> Views = Inc;
        }
    }
}
