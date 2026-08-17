using Client.Adapters.Vendor;
using Client.Simulation.MagicWords;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords
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
        public Sprite AvatarSprite => leftSide.activeSelf
            ? leftAvatar.sprite
            : rightAvatar.sprite;

        private void Awake()
        {
            _HasEveryReference();
        }

        public void Configure(
            string speakerName,
            AvatarSide side,
            Sprite bubbleSprite,
            Sprite frameSprite,
            TMP_SpriteAsset emoji,
            string body)
        {
            var isLeft = side == AvatarSide.Left;
            leftSide.SetActive(isLeft);
            rightSide.SetActive(isLeft == false);
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
            if (itemData is not DialogueLineItemData data)
            {
                Debug.LogError($"{nameof(DialogueLineView)} received unexpected item data " +
                               $"'{itemData?.GetType().Name ?? "null"}'.", this);
                return;
            }

            Configure(data.SpeakerName, data.Side, data.Bubble, data.Frame, data.Emoji, data.Body);
            SetAvatar(data.Avatar);
        }

        /// <summary>VList recycling: a pooled line must not inherit a mid-fade alpha.</summary>
        public void OnHide()
        {
            group.alpha = 1f;
        }

        public void SetAvatar(Sprite sprite)
        {
            if (leftSide.activeSelf)
                leftAvatar.sprite = sprite;
            else
                rightAvatar.sprite = sprite;
        }

        private bool _HasEveryReference()
        {
            var isComplete = true;
            isComplete &= _Check(group, nameof(group));
            isComplete &= _Check(leftSide, nameof(leftSide));
            isComplete &= _Check(rightSide, nameof(rightSide));
            isComplete &= _Check(leftAvatar, nameof(leftAvatar));
            isComplete &= _Check(rightAvatar, nameof(rightAvatar));
            isComplete &= _Check(leftFrame, nameof(leftFrame));
            isComplete &= _Check(rightFrame, nameof(rightFrame));
            isComplete &= _Check(bubble, nameof(bubble));
            isComplete &= _Check(speakerLabel, nameof(speakerLabel));
            isComplete &= _Check(bodyLabel, nameof(bodyLabel));
            return isComplete;
        }

        private bool _Check(Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            Debug.LogError($"{fieldName} is not assigned on {nameof(DialogueLineView)}.", this);
            return false;
        }
    }
}
