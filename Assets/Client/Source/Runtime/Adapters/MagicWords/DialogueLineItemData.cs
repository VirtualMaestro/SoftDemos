using Client.Adapters.Vendor.OptVList;
using Client.Simulation.MagicWords;
using TMPro;
using UnityEngine;

namespace Client.Adapters.MagicWords
{
    /// <summary>One dialogue line for the <see cref="VList"/>. <c>DialogueLineView.OnShow</c> draws it.</summary>
    /// <remarks>
    /// It carries only what the view draws. The avatar poll state moved to
    /// <c>DialogueLineViewComp</c> on the line's own entity, so a record is built for a draw and
    /// kept by nobody (adr-data-placement-is-decided-on-three-axes rule 4).
    /// </remarks>
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

        public int ItemId { get; set; }
    }
}
