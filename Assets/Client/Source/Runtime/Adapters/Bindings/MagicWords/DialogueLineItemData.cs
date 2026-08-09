using Game.Simulation.MagicWords;
using TMPro;
using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Per-line payload handed to the dialogue <see cref="VList"/>; DialogueLineView.OnShow
    /// renders it. Also carries the per-line avatar-poll bookkeeping DialogueLogSystem keeps,
    /// so one object per line replaces the old view binding record.
    /// </summary>
    public sealed class DialogueLineItemData : IItemData
    {
        public int EntityId;
        public int SpeakerId;
        public string SpeakerName;
        public AvatarSide Side;
        public Sprite Bubble;
        public Sprite Frame;
        public TMP_SpriteAsset Emoji;
        public string Body;
        public Sprite Avatar;

        // Avatar poll bookkeeping (system-owned, never rendered).
        public AvatarLoadState LastState = (AvatarLoadState)(-1);
        public int LastHandleId = -1;

        public int ItemId { get; set; }
    }
}
