using Game.Adapters.Views;
using TMPro;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Magic Words dialogue-log content and reset signal, shared between
    /// <see cref="MagicWordsStageSystem"/> (writer) and <see cref="DialogueLogSystem"/> (reader).
    /// A plain collaborator rather than direct calls between the two systems, because systems must
    /// never hold other systems (see SystemIsolationTests). The reset is a version counter, not a
    /// callback: the stage system bumps it during teardown, and the log system — which runs later
    /// in the same LateRun pass — destroys its own views when it sees the change, so the clear
    /// stays same-frame. Sprite and asset lifetimes stay with the stage system.
    /// </summary>
    public sealed class DialogueLogChannel
    {
        public TMP_SpriteAsset Emoji { get; private set; }
        public Sprite Bubble { get; private set; }
        public Sprite Frame { get; private set; }
        public Sprite Placeholder { get; private set; }
        public MagicWordsScreen Scene { get; private set; }

        /// <summary>Bumped on every teardown; the dialogue log clears its views on change.</summary>
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
