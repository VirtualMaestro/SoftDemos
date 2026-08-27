using Client.Adapters.MagicWords.Views;
using TMPro;
using UnityEngine;

namespace Client.Adapters.MagicWords
{
    /// <summary>Shared dialogue-log content. The stage system writes, the log system reads.</summary>
    /// <remarks>
    /// Content only: the stage system owns the sprites and the assets, and the teardown signal the
    /// log system acts on travels through the world as <c>DialogueLogResetEvent</c>.
    /// </remarks>
    public sealed class DialogueLogChannel
    {
        public TMP_SpriteAsset Emoji { get; private set; }
        public Sprite Bubble { get; private set; }
        public Sprite Frame { get; private set; }
        public Sprite Placeholder { get; private set; }
        public MagicWordsScreen Scene { get; private set; }

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
        }
    }
}
