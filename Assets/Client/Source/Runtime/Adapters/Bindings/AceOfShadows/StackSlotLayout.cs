using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Places two 1.7-unit-wide card stacks inside an orthographic viewport. At the reference
    /// size of five, portrait spacing is 2.3 units.
    /// </summary>
    public sealed class StackSlotLayout
    {
        /// <summary>
        /// Rise per card in the visible part of the pile, chosen so the sliver each card shows is
        /// wider than its own dark border: ~10 px landscape, ~17 px portrait. Spreading all 143
        /// depth steps over a fixed 1.6-unit budget instead gave every card ~2 px, which is border
        /// and nothing else, so 140 of them merged into one black bar. Deliberately looser than a
        /// real deck of 144 would be — realism here reads as a smudge.
        /// </summary>
        public const float PerCardOffset = 0.09f;

        /// <summary>
        /// Depth steps beyond this one land on the same spot. At this offset a 143-card pile would
        /// otherwise stand nearly thirteen units tall — the viewport is ten — so only the top of
        /// the deck spreads out and everything below sits under it, hidden exactly as the cards in
        /// the middle of a real deck are. The pile therefore holds its height until the last dozen
        /// cards, which is where the counters carry the progress instead.
        /// The z step is deliberately left uncapped so draw order still follows depth exactly.
        /// </summary>
        public const int VisibleDepth = 12;

        private readonly Vector3[] _slots = new Vector3[2];

        public int Version { get; private set; }

        public void Recalculate(int screenWidth, int screenHeight, float orthographicSize)
        {
            var aspect = screenHeight > 0 ? screenWidth / (float)screenHeight : 1f;
            var scale = orthographicSize / 5f;
            var halfWidth = orthographicSize * aspect;
            var portrait = aspect < 1f;
            var desiredSpacing = (portrait ? 2.3f : 5f) * scale;
            var maximumSpacing = Mathf.Max(0f, (halfWidth - 0.85f * scale) * 2f);
            var spacing = Mathf.Min(desiredSpacing, maximumSpacing);
            var baseline = (portrait ? -1.4f : -1f) * scale;

            _slots[0] = new Vector3(-spacing * 0.5f, baseline, 0f);
            _slots[1] = new Vector3(spacing * 0.5f, baseline, 0f);
            Version++;
        }

        public Vector3 SlotPosition(int slotIndex, int depth)
        {
            if ((uint)slotIndex >= (uint)_slots.Length)
                return default;

            var visibleDepth = depth < VisibleDepth ? depth : VisibleDepth;
            return _slots[slotIndex] + new Vector3(0f, visibleDepth * PerCardOffset, -depth * 0.001f);
        }
    }
}
