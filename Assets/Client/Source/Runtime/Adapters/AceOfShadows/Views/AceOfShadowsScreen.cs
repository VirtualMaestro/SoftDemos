using TMPro;
using Client.Adapters.Shared;
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
        // Optional: a screen that carries none falls back to Camera.main.
        [Optional] [SerializeField] private Camera stageCamera;

        public Transform CardRoot => cardRoot;
        public SpriteRenderer Background => background;
        public TMP_Text SourceCounter => sourceCounter;
        public TMP_Text TargetCounter => targetCounter;
        public TMP_Text CompletionLabel => completionLabel;
        public TMP_Text SpeedLabel => speedLabel;
        public CardView CardPrefab => cardPrefab;

        /// <summary>
        /// The camera the background is cover-fitted to. It lives on the screen rather than cached
        /// on the stage system: a camera is a <c>UnityEngine.Object</c> and a system holds none
        /// (DEU0146). Left unassigned it falls back to <see cref="Camera.main"/>, which is what the
        /// cache used to do on its first frame.
        /// </summary>
        public Camera StageCamera => stageCamera;

        /// <summary>
        /// The speed button's own target graphic, so the stage can skin it with the shared atlas
        /// sprite. Taken from the Button rather than serialized separately — a second field for the
        /// image the Button already points at is one more thing to leave unassigned.
        /// </summary>
        public Image SpeedButtonImage => speedButton != null ? speedButton.image : null;

        /// <summary>Set by the speed button, cleared by the system that drains it into a command.</summary>
        /// <remarks>
        /// A pending press is not world state: it has no lifetime, no owner and no reader but the
        /// one Input system. The view holds it until that system's phase runs, which is what makes
        /// the frame the command lands in readable — a C# event fired straight into a command
        /// wrote the world from uGUI's callback, at whichever point in the frame uGUI chose.
        /// </remarks>
        public bool SpeedRequested { get; set; }

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
            SpeedRequested = true;
        }

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
    }
}
