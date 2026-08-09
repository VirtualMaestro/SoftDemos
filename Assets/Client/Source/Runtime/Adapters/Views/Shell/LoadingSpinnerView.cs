using UnityEngine;

namespace Game.Adapters.Views
{
    /// <summary>
    /// Spins the loading indicator. A spinner that does not spin reads as a hang.
    ///
    /// Deliberately not a tween: <c>TweenPlaybackSystem</c> is the project's single DOTween call
    /// site, and a looping rotation needs none of what a tween buys. It also needs no stop logic —
    /// <c>ScreenPresentationSystem</c> deactivates the indicator, and <c>Update</c> stops with it.
    /// <c>unscaledDeltaTime</c> so the spinner survives a paused or slowed timescale.
    /// </summary>
    public sealed class LoadingSpinnerView : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = -180f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
