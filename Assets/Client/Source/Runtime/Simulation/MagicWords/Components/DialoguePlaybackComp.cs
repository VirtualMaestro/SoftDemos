using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// How far the reveal has got: how many lines are on screen, how long until the next one and whether
    /// the dialogue has finished. Kept apart from the load state because a reload restarts the reveal.
    /// </summary>
    /// <remarks>
    /// Owned by <c>DialoguePlaybackSystem</c>, which is the only writer, and zeroed by
    /// <c>DialogueResetSystem</c>. <c>IsComplete</c> is what stops the timer from running forever after
    /// the last line. It lives as long as the world does.
    /// </remarks>
    public struct DialoguePlaybackComp : IEcsWorldComponent<DialoguePlaybackComp>
    {
        public int VisibleLineCount;
        public float SecondsUntilNextLine;
        public bool IsComplete;

        void IEcsWorldComponent<DialoguePlaybackComp>.Init(
            ref DialoguePlaybackComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialoguePlaybackComp>.OnDestroy(
            ref DialoguePlaybackComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString()
        {
            return $"{nameof(VisibleLineCount)}={VisibleLineCount}, " +
                $"{nameof(SecondsUntilNextLine)}={SecondsUntilNextLine}, " +
                $"{nameof(IsComplete)}={IsComplete}";
        }
    }
}
