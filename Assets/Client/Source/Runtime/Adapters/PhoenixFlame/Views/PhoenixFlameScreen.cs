using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.PhoenixFlame.Views
{
    /// <summary>
    /// The Phoenix Flame demo scene publishing itself to the stage system. It holds no game state
    /// and makes no decisions: the button raises an event, the stage decides what it means.
    /// </summary>
    public sealed class PhoenixFlameScreen : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private Animator flameAnimator;
        [SerializeField] private FlameColorView flameColor;
        [SerializeField] private Button advanceButton;
        [SerializeField] private TMP_Text phaseLabel;

        public SpriteRenderer Background => background;
        public Animator FlameAnimator => flameAnimator;
        public FlameColorView FlameColor => flameColor;
        public Button AdvanceButton => advanceButton;
        public TMP_Text PhaseLabel => phaseLabel;

        public event Action OnAdvancePressed;

        private void OnValidate()
        {
            Debug.Assert(background != null, $"'{nameof(background)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(flameAnimator != null, $"'{nameof(flameAnimator)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(flameColor != null, $"'{nameof(flameColor)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(advanceButton != null, $"'{nameof(advanceButton)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(phaseLabel != null, $"'{nameof(phaseLabel)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
        }

        private void Awake()
        {
            advanceButton.onClick.AddListener(_OnAdvancePressed);
        }

        private void OnDestroy()
        {
            // `?.` bypasses Unity's null overload, so a destroyed button would slip past it.
            if (advanceButton != null)
                advanceButton.onClick.RemoveListener(_OnAdvancePressed);
        }

        private void _OnAdvancePressed()
        {
            OnAdvancePressed?.Invoke();
        }
    }
}
