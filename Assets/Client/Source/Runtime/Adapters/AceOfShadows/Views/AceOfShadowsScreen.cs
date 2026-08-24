using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.AceOfShadows.Views
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

        private void OnValidate()
        {
            Debug.Assert(cardRoot != null, $"'{nameof(cardRoot)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(background != null, $"'{nameof(background)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(sourceCounter != null, $"'{nameof(sourceCounter)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(targetCounter != null, $"'{nameof(targetCounter)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(completionLabel != null, $"'{nameof(completionLabel)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(speedButton != null, $"'{nameof(speedButton)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(speedLabel != null, $"'{nameof(speedLabel)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
            Debug.Assert(cardPrefab != null, $"'{nameof(cardPrefab)}' is not assigned on {nameof(AceOfShadowsScreen)}.", this);
        }

        private void Awake()
        {
            speedButton.onClick.AddListener(_OnSpeedButtonPressed);
        }

        private void OnDestroy()
        {
            if (speedButton != null)
                speedButton.onClick.RemoveListener(_OnSpeedButtonPressed);
        }

        private void _OnSpeedButtonPressed()
        {
            OnSpeedButtonPressed?.Invoke();
        }
    }
}
