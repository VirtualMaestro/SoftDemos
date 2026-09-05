using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Client.Adapters.Shared.Services
{
    /// <summary>Plays canvas-group fades through DOTween. The shared half of tween playback.</summary>
    /// <remarks>
    /// This is not a system. Several systems share the behaviour, so the composition root owns it,
    /// like <see cref="ScreenRegistryService"/>. Card moves live in
    /// <see cref="AceOfShadows.Services.CardMovePlayerService"/> — they are one feature's
    /// behaviour, and this service holds nothing a feature owns.
    /// An owner disposes what it hands out: the composition root disposes this service, and no
    /// system releases a fade on destroy.
    /// </remarks>
    public sealed class FadePlayerService : IDisposable
    {
        private readonly List<CanvasGroup> _fading = new();

        public void FadeIn(CanvasGroup group, float duration)
        {
            group.alpha = 0f;
            _fading.Add(group);
            DOTween.To(() => group.alpha, value => group.alpha = value, 1f, duration)
                .SetTarget(group)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _fading.Remove(group));
        }

        public void KillFades()
        {
            foreach (var group in _fading)
                if (group != null)
                    DOTween.Kill(group);

            _fading.Clear();
        }

        /// <summary>Kills every fade before the world goes away.</summary>
        public void Dispose() => KillFades();

        // Properties are used only for tests
        internal int ActiveFadeCount => _fading.Count;
    }
}
