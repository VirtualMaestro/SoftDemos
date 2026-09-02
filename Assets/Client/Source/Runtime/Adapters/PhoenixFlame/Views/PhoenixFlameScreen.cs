using TMPro;
using Client.Adapters.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.PhoenixFlame.Views
{
    /// <summary>
    /// The Phoenix Flame demo scene publishing itself to the systems that drive it. It holds no
    /// game state and makes no decisions: the button records the press, the Input system decides
    /// what it means.
    /// </summary>
    public sealed class PhoenixFlameScreen : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private Animator flameAnimator;
        [SerializeField] private FlameColorView flameColor;
        [SerializeField] private Button advanceButton;
        [SerializeField] private TMP_Text phaseLabel;
        // Optional: a screen that carries none falls back to Camera.main.
        [Optional] [SerializeField] private Camera stageCamera;

        public SpriteRenderer Background => background;
        public Animator FlameAnimator => flameAnimator;
        public FlameColorView FlameColor => flameColor;
        public Button AdvanceButton => advanceButton;
        public TMP_Text PhaseLabel => phaseLabel;

        /// <summary>
        /// The camera the background is cover-fitted to. It lives on the screen rather than cached
        /// on the stage system: a camera is a <c>UnityEngine.Object</c> and a system holds none
        /// (DEU0146). Left unassigned it falls back to <see cref="Camera.main"/>.
        /// </summary>
        public Camera StageCamera => stageCamera;

        /// <summary>Set by the advance button, cleared by the system that drains it into a command.</summary>
        /// <remarks>
        /// A pending press is not world state: it has no lifetime, no owner and no reader but the
        /// one Input system. The view holds it until that system's phase runs, which is what makes
        /// the frame the command lands in readable.
        /// </remarks>
        public bool AdvanceRequested { get; set; }

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
            AdvanceRequested = true;
        }

        private void OnValidate()
        {
            Debug.Assert(background != null, $"'{nameof(background)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(flameAnimator != null, $"'{nameof(flameAnimator)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(flameColor != null, $"'{nameof(flameColor)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(advanceButton != null, $"'{nameof(advanceButton)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
            Debug.Assert(phaseLabel != null, $"'{nameof(phaseLabel)}' is not assigned on {nameof(PhoenixFlameScreen)}.", this);
        }
    }
}
