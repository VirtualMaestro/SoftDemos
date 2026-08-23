using Client.Simulation.MagicWords.Payload;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// The raw JSON payload between the fetch that received it and the ingest that turns it into
    /// entities. It is a hand-off slot, not storage: ingest clears it once the entities exist.
    /// </summary>
    /// <remarks>
    /// Written by <c>DialogueFetchSystem</c> when the request resolves, read and reset to default by
    /// <c>DialogueIngestSystem</c> in the same or a later tick. <c>DialogueResetSystem</c> also zeroes it
    /// so a payload never survives a teardown.
    /// </remarks>
    public struct DialoguePayloadComp : IEcsWorldComponent<DialoguePayloadComp>
    {
        public DialoguePayload Payload;

        void IEcsWorldComponent<DialoguePayloadComp>.Init(ref DialoguePayloadComp component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialoguePayloadComp>.OnDestroy(ref DialoguePayloadComp component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString() =>
            $"{nameof(Payload)}={(Payload == null ? "null" : "loaded")}";
    }
}
