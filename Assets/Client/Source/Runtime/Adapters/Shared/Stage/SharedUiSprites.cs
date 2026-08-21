using UnityEngine;

namespace Client.Adapters.Shared.Stage
{
    /// <summary>Sprites the shell lends to a demo, so the demo needs no second request on the same atlas.</summary>
    /// <remarks>
    /// The shell owns and destroys these copies. Boot stays loaded, so they outlive every demo.
    /// They are null until the shell finishes loading. A consumer must then keep its own look.
    /// </remarks>
    public sealed class SharedUiSprites
    {
        /// <summary>The one <c>ui-button</c> copy every shell button shares.</summary>
        public Sprite Button { get; set; }
    }
}
