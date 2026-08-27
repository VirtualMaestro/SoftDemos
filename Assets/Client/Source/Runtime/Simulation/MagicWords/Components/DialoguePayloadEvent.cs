using Client.Simulation.MagicWords.Payload;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// The raw JSON payload, handed from the fetch that received it to the ingest that turns it into
    /// entities. An event, not storage: it is filled and emptied inside one hand-off and holds
    /// nothing between them.
    /// </summary>
    /// <remarks>
    /// Written by <c>DialogueFetchSystem</c> when the request resolves, read and reset to default by
    /// <c>DialogueIngestSystem</c>. It is a world component rather than an entity's, so it is set and
    /// cleared in place instead of added and deleted — the lifetime the name states is the one the
    /// two systems keep. <c>DialogueResetSystem</c> also zeroes it so a payload never survives a
    /// teardown.
    /// </remarks>
    internal struct DialoguePayloadEvent : IEcsWorldComponent<DialoguePayloadEvent>
    {
        public DialoguePayload Payload;

        void IEcsWorldComponent<DialoguePayloadEvent>.Init(ref DialoguePayloadEvent component, EcsWorld world)
        {
            component = default;
        }

        void IEcsWorldComponent<DialoguePayloadEvent>.OnDestroy(ref DialoguePayloadEvent component, EcsWorld world)
        {
            component = default;
        }

        public override string ToString() =>
            $"{nameof(Payload)}={(Payload == null ? "null" : "loaded")}";
    }
}
