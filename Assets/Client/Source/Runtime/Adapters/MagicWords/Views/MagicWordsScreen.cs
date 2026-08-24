using System;
using Client.Adapters.Vendor.OptVList;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords.Views
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

        private void OnValidate()
        {
            Debug.Assert(background != null, $"'{nameof(background)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
            Debug.Assert(logScroll != null, $"'{nameof(logScroll)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
            Debug.Assert(logContent != null, $"'{nameof(logContent)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
            Debug.Assert(logList != null, $"'{nameof(logList)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
            Debug.Assert(avatarModeButton != null, $"'{nameof(avatarModeButton)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
            Debug.Assert(avatarModeLabel != null, $"'{nameof(avatarModeLabel)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
            Debug.Assert(statusLabel != null, $"'{nameof(statusLabel)}' is not assigned on {nameof(MagicWordsScreen)}.", this);
        }

        private void Awake()
        {
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
    }
}
