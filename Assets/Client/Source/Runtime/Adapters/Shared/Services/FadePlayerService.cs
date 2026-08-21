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
    /// </remarks>
    public sealed class FadePlayerService
    {
        private readonly List<CanvasGroup> _fading = new();

        public int ActiveFadeCount => _fading.Count;

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
    }
}
