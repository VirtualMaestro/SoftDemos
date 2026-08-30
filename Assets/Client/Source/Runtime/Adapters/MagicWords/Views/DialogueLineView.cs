using Client.Adapters.Vendor.OptVList;
using Client.Simulation.MagicWords;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords.Views
{
    public sealed class DialogueLineView : MonoBehaviour, IItemVisual
    {
        [SerializeField] private CanvasGroup group;

        // The side roots, not the frame images. uGUI draws a child after its parent, so the frame
        // ring has to be a *sibling* placed after the avatar rather than the avatar's parent —
        // which means the object that gets switched on and off is no longer the frame itself.
        [SerializeField] private GameObject leftSide;
        [SerializeField] private GameObject rightSide;
        [SerializeField] private Image leftAvatar;
        [SerializeField] private Image rightAvatar;
        [SerializeField] private Image leftFrame;
        [SerializeField] private Image rightFrame;
        [SerializeField] private Image bubble;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyLabel;

        public CanvasGroup Group => group;

        private void _Configure(
            string speakerName,
            AvatarSide side,
            Sprite bubbleSprite,
            Sprite frameSprite,
            TMP_SpriteAsset emoji,
            string body)
        {
            var isLeft = side == AvatarSide.Left;
            leftSide.SetActive(isLeft);
            rightSide.SetActive(!isLeft);
            leftFrame.sprite = frameSprite;
            rightFrame.sprite = frameSprite;
            bubble.sprite = bubbleSprite;
            speakerLabel.text = speakerName;
            bodyLabel.spriteAsset = emoji;
            bodyLabel.text = body;
        }

        /// <summary>VList binding: renders the pooled line from its data record.</summary>
        public void OnShow(IItemData itemData)
        {
            var data = (DialogueLineItemData) itemData ;

            _Configure(data.SpeakerName, data.Side, data.Bubble, data.Frame, data.Emoji, data.Body);
            _SetAvatar(data.Avatar);
        }

        /// <summary>VList recycling: a pooled line must not inherit a mid-fade alpha.</summary>
        public void OnHide()
        {
            group.alpha = 1f;
        }

        private void _SetAvatar(Sprite sprite)
        {
            if (leftSide.activeSelf)
                leftAvatar.sprite = sprite;
            else
                rightAvatar.sprite = sprite;
        }

        private void OnValidate()
        {
            Debug.Assert(group != null, $"'{nameof(group)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(leftSide != null, $"'{nameof(leftSide)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(rightSide != null, $"'{nameof(rightSide)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(leftAvatar != null, $"'{nameof(leftAvatar)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(rightAvatar != null, $"'{nameof(rightAvatar)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(leftFrame != null, $"'{nameof(leftFrame)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(rightFrame != null, $"'{nameof(rightFrame)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(bubble != null, $"'{nameof(bubble)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(speakerLabel != null, $"'{nameof(speakerLabel)}' is not assigned on {nameof(DialogueLineView)}.", this);
            Debug.Assert(bodyLabel != null, $"'{nameof(bodyLabel)}' is not assigned on {nameof(DialogueLineView)}.", this);
        }
    }
}
