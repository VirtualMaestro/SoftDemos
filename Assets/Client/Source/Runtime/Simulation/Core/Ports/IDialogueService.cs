using Game.Simulation.MagicWords;

namespace Game.Simulation.Ports
{
    /// <summary>
    /// Dialogue payload loading as a handle-and-poll port.
    ///
    /// The port returns plain simulation DTOs and never exposes an engine or transport object.
    /// </summary>
    public interface IDialogueService
    {
        /// <summary>Starts loading the dialogue payload.</summary>
        int BeginLoad();

        /// <summary>
        /// Current status of <paramref name="requestId"/>. An unknown or already released id
        /// reads as <see cref="AsyncOpStatus.Pending"/> — polling never throws.
        /// </summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>
        /// Loaded payload, valid only while the request is <see cref="AsyncOpStatus.Done"/>.
        /// Returns <c>null</c> in every other case.
        /// </summary>
        DialoguePayload Resolve(int requestId);

        /// <summary>Releases the request and its transport resources.</summary>
        void Release(int requestId);
    }
}
