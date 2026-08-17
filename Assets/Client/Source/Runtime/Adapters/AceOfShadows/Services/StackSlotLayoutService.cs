using UnityEngine;

namespace Client.Adapters.AceOfShadows
{
    /// <summary>
    /// Places two card stacks inside an orthographic viewport. All authored values below are in
    /// world units.
    /// </summary>
    public sealed class StackSlotLayoutService
    {
        /// <summary>Camera size the authored values were tuned at.</summary>
        private const float ReferenceOrthoSize = 5f;

        /// <summary>Width of one card. Keeps a whole stack inside the viewport edge.</summary>
        private const float CardWidth = 1.7f;

        /// <summary>Gap between the two stacks in portrait, where width is scarce.</summary>
        private const float PortraitStackGap = 0.6f;

        /// <summary>Distance between stack centers in landscape. Tuned by eye.</summary>
        private const float LandscapeSpacing = 5f;

        /// <summary>Stack baseline. Portrait sits lower so the pile clears the HUD. Tuned by eye.</summary>
        private const float PortraitBaselineY = -1.4f;
        private const float LandscapeBaselineY = -1f;

        /// <summary>Depth offset per card, so draw order follows depth.</summary>
        private const float ZStepPerCard = 0.001f;

        /// <summary>Rise per card in the visible part of the pile.</summary>
        /// <remarks>
        /// Each card shows about 10 px in landscape and 17 px in portrait, which is wider than its
        /// own border. A smaller step merges the cards into one dark bar.
        /// </remarks>
        private const float PerCardOffset = 0.09f;

        /// <summary>Depth steps above this one land on the same spot.</summary>
        /// <remarks>
        /// Without the cap a full pile stands about thirteen units tall and the viewport is ten.
        /// Only the top of the deck spreads out. The z step stays uncapped, so draw order still
        /// follows depth.
        /// </remarks>
        private const int VisibleDepth = 12;

        private readonly Vector3[] _slots = new Vector3[2];

        public int Version { get; private set; }

        public void Recalculate(int screenWidth, int screenHeight, float orthographicSize)
        {
            var aspect = screenHeight > 0 ? screenWidth / (float)screenHeight : 1f;
            var scale = orthographicSize / ReferenceOrthoSize;
            var halfWidth = orthographicSize * aspect;
            var portrait = aspect < 1f;
            var desiredSpacing = (portrait ? CardWidth + PortraitStackGap : LandscapeSpacing) * scale;
            // Both stacks stay whole inside the viewport: half a card fits between each slot
            // center and the screen edge.
            var maximumSpacing = Mathf.Max(0f, (halfWidth - CardWidth / 2f * scale) * 2f);
            var spacing = Mathf.Min(desiredSpacing, maximumSpacing);
            var baseline = (portrait ? PortraitBaselineY : LandscapeBaselineY) * scale;

            _slots[0] = new Vector3(-spacing * 0.5f, baseline, 0f);
            _slots[1] = new Vector3(spacing * 0.5f, baseline, 0f);
            Version++;
        }

        public Vector3 SlotPosition(int slotIndex, int depth)
        {
            if ((uint)slotIndex >= (uint)_slots.Length)
                return default;

            var visibleDepth = depth < VisibleDepth ? depth : VisibleDepth;
            return _slots[slotIndex] + new Vector3(0f, visibleDepth * PerCardOffset, -depth * ZStepPerCard);
        }
    }
}
