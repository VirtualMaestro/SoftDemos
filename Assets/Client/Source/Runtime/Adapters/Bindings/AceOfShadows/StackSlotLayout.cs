using UnityEngine;

namespace Game.Adapters.Bindings
{
    /// <summary>
    /// Places two 1.7-unit-wide card stacks inside an orthographic viewport. At the reference
    /// size of five, portrait spacing is 2.3 units.
    /// </summary>
    public sealed class StackSlotLayout
    {
        /// <summary>Rise per card in the visible part of the pile.</summary>
        /// <remarks>
        /// Each card shows about 10 px in landscape and 17 px in portrait, which is wider than its
        /// own border. A smaller step merges the cards into one dark bar.
        /// </remarks>
        public const float PerCardOffset = 0.09f;

        /// <summary>Depth steps above this one land on the same spot.</summary>
        /// <remarks>
        /// Without the cap a full pile stands about thirteen units tall and the viewport is ten.
        /// Only the top of the deck spreads out. The z step stays uncapped, so draw order still
        /// follows depth.
        /// </remarks>
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
