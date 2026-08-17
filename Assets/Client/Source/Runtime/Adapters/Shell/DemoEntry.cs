using System;

namespace Client.Adapters.Shell
{
    /// <summary>One menu entry: the scene it opens and the name the button and the HUD show.</summary>
    /// <remarks>Author the list once, on <c>EntryPoint</c>. One ordered list cannot disagree with itself.</remarks>
    [Serializable]
    public sealed class DemoEntry
    {
        public string Address;
        public string Title;

        /// <summary>Name of the icon inside <c>art/menu/ui-atlas</c>. Not a <c>Sprite</c>.</summary>
        /// <remarks>A serialized sprite would pull the source image into the scene dependencies.</remarks>
        public string IconName;
    }
}
