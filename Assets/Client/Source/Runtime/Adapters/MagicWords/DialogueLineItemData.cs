using Client.Adapters.Vendor;
using Client.Simulation.MagicWords;
using TMPro;
using UnityEngine;

namespace Client.Adapters.MagicWords
{
    /// <summary>One dialogue line for the <see cref="VList"/>. <c>DialogueLineView.OnShow</c> draws it.</summary>
    /// <remarks>It also carries the avatar poll state, so there is one object per line.</remarks>
    public sealed class DialogueLineItemData : IItemData
    {
        public int SpeakerId;
        public string SpeakerName;
        public AvatarSide Side;
        public Sprite Bubble;
        public Sprite Frame;
        public TMP_SpriteAsset Emoji;
        public string Body;
        public Sprite Avatar;

        // Avatar poll state. The system owns it and nothing draws it.
        public AvatarLoadState LastState = (AvatarLoadState)(-1);
        public int LastHandleId = -1;

        public int ItemId { get; set; }
    }
}
