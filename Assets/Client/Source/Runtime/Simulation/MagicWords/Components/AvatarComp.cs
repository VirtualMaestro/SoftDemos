using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// The avatar the payload declared for this speaker: where to download it and which side of the log
    /// their bubbles sit on. Absent when the payload lists no avatar for the name.
    /// </summary>
    /// <remarks>
    /// Written by <c>DialogueIngestSystem</c> while it creates the speaker. Read by
    /// <c>AvatarLoadSystem</c> to start the download and by <c>DialogueLogSystem</c> to lay the bubble
    /// out. Removed only when the speaker entity is deleted on reset.
    /// </remarks>
    public struct AvatarComp : IEcsComponent
    {
        public string Url;
        public AvatarSide Side;
    }
}
