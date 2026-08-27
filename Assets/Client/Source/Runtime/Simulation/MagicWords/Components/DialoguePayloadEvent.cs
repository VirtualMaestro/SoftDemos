using Client.Simulation.MagicWords.Payload;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// The raw JSON payload, handed from the fetch that received it to the ingest that turns it into
    /// entities. One frame long: it exists for that hand-off and nothing else.
    /// </summary>
    /// <remarks>
    /// Added on its own entity by <c>DialogueFetchSystem</c> when the request resolves, read by
    /// <c>DialogueIngestSystem</c> in the same tick, and deleted by <c>DialogueCleanupSystem</c> at
    /// the end of the frame — which is the whole of its lifetime, stated by the suffix and enforced
    /// in one place.
    /// <para>It was a world component set and cleared in place until the phases arrived. That
    /// version worked and was invisible: no <c>Add</c>, no <c>Del</c>, so no rule could see it and
    /// no reader could tell the hand-off from storage.</para>
    /// </remarks>
    internal struct DialoguePayloadEvent : IEcsComponent
    {
        public DialoguePayload Payload;

        public override string ToString() =>
            $"{nameof(Payload)}={(Payload == null ? "null" : "loaded")}";
    }
}
