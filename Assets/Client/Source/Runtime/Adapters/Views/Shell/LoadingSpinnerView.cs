using UnityEngine;

namespace Client.Adapters.Views
{
    /// <summary>Rotates the loading indicator.</summary>
    /// <remarks>
    /// This is not a tween. A loop needs nothing a tween gives, and it needs no stop logic:
    /// the screen deactivates the indicator and <c>Update</c> stops with it. It uses
    /// <c>unscaledDeltaTime</c>, so it keeps turning at any timescale.
    /// </remarks>
    public sealed class LoadingSpinnerView : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = -180f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
