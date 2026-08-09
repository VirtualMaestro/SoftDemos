using Game.Simulation.MagicWords;

namespace Game.Simulation.Ports
{
    /// <summary>Loads the dialogue payload. Handle and poll.</summary>
    /// <remarks>The port returns plain simulation types, never an engine or transport object.</remarks>
    public interface IDialogueService
    {
        /// <summary>Starts to load the dialogue payload.</summary>
        int BeginLoad();

        /// <summary>Status of <paramref name="requestId"/>. An unknown id reads as Pending.</summary>
        AsyncOpStatus Poll(int requestId);

        /// <summary>The payload. Valid only while the request is Done, else <c>null</c>.</summary>
        DialoguePayload Resolve(int requestId);

        /// <summary>Releases the request and its transport resources.</summary>
        void Release(int requestId);
    }
}
