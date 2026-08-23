using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Components
{
    /// <summary>
    /// Identifies one participant of the dialogue. Speakers are entities rather than a string on every
    /// line, because a single avatar load has to be shared by all the lines that person says.
    /// </summary>
    /// <remarks>
    /// Created by <c>DialogueIngestSystem</c>, once per distinct name in the payload. Read by
    /// <c>AvatarLoadSystem</c> for the log message and by <c>DialogueLogSystem</c> for the name on the
    /// bubble. Removed only when <c>DialogueResetSystem</c> deletes the speaker entity.
    /// </remarks>
    public struct SpeakerComp : IEcsComponent
    {
        public string Name;
    }
}
