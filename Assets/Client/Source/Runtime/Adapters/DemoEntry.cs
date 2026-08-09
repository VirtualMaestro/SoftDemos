using System;

namespace Game.Adapters
{
    /// <summary>
    /// One menu entry: the addressable scene it opens and the name both the menu button and the
    /// demo HUD show for it.
    ///
    /// Authored once, on <c>EntryPoint</c>. Before this existed the same ordering lived in three
    /// places — a button's click handler, the serialized address array and a switch over titles —
    /// so reordering the addresses in the inspector silently made one entry open another entry's
    /// scene under a third entry's name. Nothing failed and no test noticed. One ordered list
    /// cannot disagree with itself.
    /// </summary>
    [Serializable]
    public sealed class DemoEntry
    {
        public string Address;
        public string Title;

        /// <summary>
        /// Name of this entry's icon inside <c>art/menu/ui-atlas</c>, not a <c>Sprite</c>.
        ///
        /// The atlas is loaded by address at runtime through <c>IAssetService</c>, so a serialized
        /// <c>Sprite</c> here would be a second, direct reference to content the project has
        /// already decided is addressable — and it would pull the source PNG into the scene's
        /// dependency set, which is exactly what the address indirection exists to prevent.
        /// </summary>
        public string IconName;
    }
}
