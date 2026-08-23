using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// How far this speaker's avatar download got, plus the two handle-and-poll ids it owns: the
    /// in-flight request and the loaded image. Every speaker carries one, even a speaker with no avatar.
    /// </summary>
    /// <remarks>
    /// Added by <c>DialogueIngestSystem</c> as <c>NotRequested</c> or <c>Missing</c> and driven by
    /// <c>AvatarLoadSystem</c> through Loading to Ready or Failed. <c>DialogueLogSystem</c> reads it to
    /// swap the placeholder for the real image. Reset releases the request and deletes the entity; the
    /// ids are cleared to zero first, so nothing is released twice.
    /// </remarks>
    public struct AvatarLoadComp : IEcsComponent
    {
        public AvatarLoadState State;
        public int RequestId;
        public int HandleId;
    }
}
