using System;
using Client.Adapters.Vendor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords
{
    public sealed class MagicWordsScreen : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private ScrollRect logScroll;
        [SerializeField] private RectTransform logContent;
        [SerializeField] private VList logList;
        [SerializeField] private Button avatarModeButton;
        [SerializeField] private TMP_Text avatarModeLabel;
        [SerializeField] private TMP_Text statusLabel;

        public SpriteRenderer Background => background;
        public ScrollRect LogScroll => logScroll;
        public RectTransform LogContent => logContent;
        public VList LogList => logList;
        public Button AvatarModeButton => avatarModeButton;
        public TMP_Text AvatarModeLabel => avatarModeLabel;
        public TMP_Text StatusLabel => statusLabel;

        public event Action OnSkipPressed;
        public event Action OnAvatarModePressed;

        private void Awake()
        {
            if (_HasEveryReference() == false)
                return;

            avatarModeButton.onClick.AddListener(_OnAvatarModePressed);
        }

        private void OnDestroy()
        {
            if (avatarModeButton != null)
                avatarModeButton.onClick.RemoveListener(_OnAvatarModePressed);
        }

        public void RaiseSkipPressed()
        {
            OnSkipPressed?.Invoke();
        }

        private void _OnAvatarModePressed()
        {
            OnAvatarModePressed?.Invoke();
        }

        private bool _HasEveryReference()
        {
            var isComplete = true;
            isComplete &= _Check(background, nameof(background));
            isComplete &= _Check(logScroll, nameof(logScroll));
            isComplete &= _Check(logContent, nameof(logContent));
            isComplete &= _Check(logList, nameof(logList));
            isComplete &= _Check(avatarModeButton, nameof(avatarModeButton));
            isComplete &= _Check(avatarModeLabel, nameof(avatarModeLabel));
            isComplete &= _Check(statusLabel, nameof(statusLabel));
            return isComplete;
        }

        private bool _Check(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            Debug.LogError($"{fieldName} is not assigned on {nameof(MagicWordsScreen)}.", this);
            return false;
        }
    }
}
