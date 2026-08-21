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

        private void Awake()
        {
            if (_HasEveryReference() == false)
                return;

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

        private bool _HasEveryReference()
        {
            var isComplete = true;
            isComplete &= _Check(background, nameof(background));
            isComplete &= _Check(flameAnimator, nameof(flameAnimator));
            isComplete &= _Check(flameColor, nameof(flameColor));
            isComplete &= _Check(advanceButton, nameof(advanceButton));
            isComplete &= _Check(phaseLabel, nameof(phaseLabel));
            return isComplete;
        }

        private bool _Check(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            Debug.LogError($"{fieldName} is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            return false;
        }
    }
}
