using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>Asks for every avatar image to be released and downloaded again.</summary>
    /// <remarks>
    /// Raised by <c>MagicWordsInpSystem</c> from the reload button. <c>AvatarLoadSimSystem</c> drains it
    /// at the top of its tick and answers by putting a <see cref="RequestAvatarCommand"/> on every
    /// speaker that had one.
    /// </remarks>
    public struct ReloadAvatarsCommand : IEcsComponent
    {
    }
}
