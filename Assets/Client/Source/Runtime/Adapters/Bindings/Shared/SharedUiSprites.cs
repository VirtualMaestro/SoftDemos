using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Sprites the shell lends out to demos, so a demo can skin its own UI without opening a
    /// second Addressables request on the same atlas. A plain collaborator rather than a property
    /// on <see cref="ShellStageSystem"/>, because systems must never hold other systems (see
    /// SystemIsolationTests). The shell stays owner: it created the copy, it destroys it, and
    /// because Boot never unloads its lifetime strictly contains every demo's. Null until the
    /// shell finishes loading — consumers skip skinning and keep their placeholder look.
    /// </summary>
    public sealed class SharedUiSprites
    {
        /// <summary>The one <c>ui-button</c> copy every shell button shares.</summary>
        public Sprite Button { get; set; }
    }
}
