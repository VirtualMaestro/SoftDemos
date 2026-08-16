using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.Views
{
    public sealed class AceOfShadowsScreen : MonoBehaviour
    {
        [SerializeField] private Transform cardRoot;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private TMP_Text sourceCounter;
        [SerializeField] private TMP_Text targetCounter;
        [SerializeField] private TMP_Text completionLabel;
        [SerializeField] private Button speedButton;
        [SerializeField] private TMP_Text speedLabel;
        [SerializeField] private CardView cardPrefab;

        public static AceOfShadowsScreen Current { get; private set; }

        public Transform CardRoot => cardRoot;
        public SpriteRenderer Background => background;
        public TMP_Text SourceCounter => sourceCounter;
        public TMP_Text TargetCounter => targetCounter;
        public TMP_Text CompletionLabel => completionLabel;
        public TMP_Text SpeedLabel => speedLabel;
        public CardView CardPrefab => cardPrefab;

        /// <summary>
        /// The speed button's own target graphic, so the stage can skin it with the shared atlas
        /// sprite. Taken from the Button rather than serialized separately — a second field for the
        /// image the Button already points at is one more thing to leave unassigned.
        /// </summary>
        public Image SpeedButtonImage => speedButton != null ? speedButton.image : null;

        public event Action OnSpeedButtonPressed;

        private void Awake()
        {
            if (_HasEveryReference() == false)
                return;

            speedButton.onClick.AddListener(_OnSpeedButtonPressed);
            Current = this;
        }

        private void OnDestroy()
        {
            if (speedButton != null)
                speedButton.onClick.RemoveListener(_OnSpeedButtonPressed);

            if (Current == this)
                Current = null;
        }

        private void _OnSpeedButtonPressed()
        {
            OnSpeedButtonPressed?.Invoke();
        }

        private bool _HasEveryReference()
        {
            var isComplete = true;
            isComplete &= _Check(cardRoot, nameof(cardRoot));
            isComplete &= _Check(background, nameof(background));
            isComplete &= _Check(sourceCounter, nameof(sourceCounter));
            isComplete &= _Check(targetCounter, nameof(targetCounter));
            isComplete &= _Check(completionLabel, nameof(completionLabel));
            isComplete &= _Check(speedButton, nameof(speedButton));
            isComplete &= _Check(speedLabel, nameof(speedLabel));
            isComplete &= _Check(cardPrefab, nameof(cardPrefab));
            return isComplete;
        }

        private bool _Check(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            Debug.LogError($"{fieldName} is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            return false;
        }
    }
}
