using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// How far this speaker's avatar download got, plus the id of the request that carries it.
    /// Every speaker carries one, even a speaker with no avatar.
    /// </summary>
    /// <remarks>
    /// Added by <c>DialogueIngestSimSystem</c> as <c>NotRequested</c> or <c>Missing</c> and driven by
    /// <c>AvatarLoadSimSystem</c> through Loading to Ready or Failed. <c>DialogueLogPreSystem</c> reads it to
    /// swap the placeholder for the real image. Reset releases the request and deletes the entity; the
    /// id is cleared to zero first, so nothing is released twice.
    /// <para>One id, not two: the request names the image as well, and the adapter resolves the
    /// sprite from it. See <c>IAsyncService</c>.</para>
    /// </remarks>
    public struct AvatarLoadComp : IEcsComponent
    {
        public AvatarLoadState State;
        public int RequestId;
    }
}
