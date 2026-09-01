using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// Identifies one participant of the dialogue. Speakers are entities rather than a string on every
    /// line, because a single avatar load has to be shared by all the lines that person says.
    /// </summary>
    /// <remarks>
    /// Created by <c>DialogueIngestSimSystem</c>, once per distinct name in the payload. Read by
    /// <c>AvatarLoadSimSystem</c> for the log message and by <c>DialogueLogPreSystem</c> for the name on the
    /// bubble. Removed only when <c>DialogueResetSimSystem</c> deletes the speaker entity.
    /// </remarks>
    public struct SpeakerComp : IEcsComponent
    {
        public string Name;
    }
}
