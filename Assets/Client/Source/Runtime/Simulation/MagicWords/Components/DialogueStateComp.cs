using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// Where the dialogue fetch stands and the request id it owns, plus the counts ingest produced. It
    /// is world data because the fetch is one operation for the whole screen, not per entity.
    /// </summary>
    /// <remarks>
    /// <c>DialogueFetchSystem</c> drives it from Loading to Ready or Failed, <c>DialogueIngestSystem</c>
    /// flips it to Ready and fills the counts, <c>DialogueResetSystem</c> releases a live request and
    /// zeroes it. Every other dialogue system gates on <c>State</c>. It lives as long as the world does.
    /// </remarks>
    public struct DialogueStateComp : IEcsWorldComponent<DialogueStateComp>
    {
        public DialogueLoadState State;
        public int RequestId;
        public int LineCount;
        public int SpeakerCount;

        void IEcsWorldComponent<DialogueStateComp>.Init(ref DialogueStateComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialogueStateComp>.OnDestroy(ref DialogueStateComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString()
        {
            return $"{nameof(State)}={State}, {nameof(RequestId)}={RequestId}, " +
                $"{nameof(LineCount)}={LineCount}, {nameof(SpeakerCount)}={SpeakerCount}";
        }
    }
}
