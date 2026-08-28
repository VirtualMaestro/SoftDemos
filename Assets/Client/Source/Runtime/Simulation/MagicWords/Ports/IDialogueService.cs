using Client.Simulation.MagicWords.Payload;
using Client.Simulation.Core.Ports;

namespace Client.Simulation.MagicWords.Ports
{
    /// <summary>Loads the dialogue payload. Request, poll and resolve.</summary>
    /// <remarks>
    /// The only port with a result. A <see cref="DialoguePayload"/> is plain simulation data and
    /// may cross the boundary, where a texture or a sprite may not — which is why this port
    /// inherits the resolving level of the contract and the asset ports stop one level short.
    /// </remarks>
    public interface IDialogueService : IAsyncService<DialogueRequest, DialoguePayload>
    {
    }
}
