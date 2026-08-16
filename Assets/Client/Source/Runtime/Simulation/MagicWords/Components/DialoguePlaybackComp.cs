using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords
{
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
