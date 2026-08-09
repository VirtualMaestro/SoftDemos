using Game.Adapters.Views;
using TMPro;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>Shared dialogue-log content and reset signal. The stage system writes, the log system reads.</summary>
    /// <remarks>
    /// The reset is a counter, not a callback. The stage system increments it during teardown.
    /// The log system runs later in the same pass and clears its views, so the clear is
    /// same-frame. The stage system owns the sprites and the assets.
    /// </remarks>
    public sealed class DialogueLogChannel
    {
        public TMP_SpriteAsset Emoji { get; private set; }
        public Sprite Bubble { get; private set; }
        public Sprite Frame { get; private set; }
        public Sprite Placeholder { get; private set; }
        public MagicWordsScreen Scene { get; private set; }

        /// <summary>Increments on every teardown. The dialogue log then clears its views.</summary>
        public int ResetVersion { get; private set; }

        public void SetContent(TMP_SpriteAsset emoji, Sprite bubble, Sprite frame,
            Sprite placeholder, MagicWordsScreen scene)
        {
            Emoji = emoji;
            Bubble = bubble;
            Frame = frame;
            Placeholder = placeholder;
            Scene = scene;
        }

        public void Reset()
        {
            Emoji = null;
            Bubble = null;
            Frame = null;
            Placeholder = null;
            Scene = null;
            ResetVersion++;
        }
    }
}
