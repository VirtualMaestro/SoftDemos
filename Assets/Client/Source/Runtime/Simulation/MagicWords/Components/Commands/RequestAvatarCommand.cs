using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components.Commands
{
    /// <summary>
    /// Asks for this speaker's avatar download to start. Unlike the other commands it sits on the
    /// speaker entity itself, because the request is about that one speaker.
    /// </summary>
    /// <remarks>
    /// Added by <c>DialoguePlaybackSimSystem</c> when the speaker's first line is revealed, and by
    /// <c>AvatarLoadSimSystem</c> for every loaded speaker on reload. <c>AvatarLoadSimSystem</c> removes the
    /// component — not the entity — once the download has been started.
    /// </remarks>
    internal struct RequestAvatarCommand : IEcsComponent
    {
    }
}
